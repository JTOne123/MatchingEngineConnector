﻿using System;
using System.Net;
using System.Threading.Tasks;
using Common;
using Lykke.MatchingEngine.Connector.Abstractions.Models;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.MatchingEngine.Connector.Models;
using Lykke.MatchingEngine.Connector.Tools;

namespace Lykke.MatchingEngine.Connector.Services
{
    public class TcpMatchingEngineClient : IMatchingEngineClient
    {
        private readonly TimeSpan _defaultReconnectTimeOut = TimeSpan.FromSeconds(3);

        private readonly TasksManager<long, TheResponseModel> _tasksManager =
            new TasksManager<long, TheResponseModel>();

        private readonly TasksManager<string, TheNewResponseModel> _newTasksManager =
            new TasksManager<string, TheNewResponseModel>();

        private readonly TasksManager<string, MarketOrderResponseModel> _marketOrderTasksManager =
            new TasksManager<string, MarketOrderResponseModel>();

        private readonly ClientTcpSocket<MatchingEngineSerializer, TcpOrderSocketService> _clientTcpSocket;

        private readonly object _lockObject = new object();
        private long _currentNumber = 1;

        private TcpOrderSocketService _tcpOrderSocketService;

        private long GetNextRequestId()
        {
            lock (_lockObject)
                return _currentNumber++;
        }

        public TcpMatchingEngineClient(IPEndPoint ipEndPoint, ISocketLog socketLog = null, bool ignoreErrors = false)
        {
            _clientTcpSocket = new ClientTcpSocket<MatchingEngineSerializer, TcpOrderSocketService>(
                socketLog,
                ipEndPoint,
                (int)_defaultReconnectTimeOut.TotalMilliseconds,
                () =>
                {
                    _tcpOrderSocketService = new TcpOrderSocketService(
                        _tasksManager,
                        _newTasksManager,
                        _marketOrderTasksManager,
                        socketLog,
                        ignoreErrors);
                    return _tcpOrderSocketService;
                });
        }

        public async Task UpdateBalanceAsync(string id, string clientId, string assetId, double value)
        {
            var model = MeNewUpdateBalanceModel.Create(id, clientId, assetId, value);
            var resultTask = _newTasksManager.Add(model.Id);

            await _tcpOrderSocketService.SendDataToSocket(model);

            await resultTask;
        }

        public async Task<MeResponseModel> CashInOutAsync(string id, string clientId, string assetId, double amount)
        {
            var model = MeNewCashInOutModel.Create(id, clientId, assetId, amount);
            var resultTask = _newTasksManager.Add(model.Id);

            if (!await _tcpOrderSocketService.SendDataToSocket(model))
                return null;

            var result = await resultTask;
            return result.ToDomainModel();
        }

        public async Task<MeResponseModel> CashInOutAsync(string id, string clientId, string assetId, double amount, string feeClientId, double feeSize, FeeSizeType feeSizeType)
        {
            var model = MeNewCashInOutModel.Create(id, clientId, assetId, amount, feeClientId, feeSize, feeSizeType);
            var resultTask = _newTasksManager.Add(model.Id);

            if (!await _tcpOrderSocketService.SendDataToSocket(model))
            {
                _newTasksManager.Compliete(model.Id, null);
                return null;
            }

            var result = await resultTask;
            return result.ToDomainModel();
        }

        public async Task<MeResponseModel> TransferAsync(string id, string fromClientId,
            string toClientId, string assetId, double amount, string feeClientId, double feeSizePercentage, double overdraft)
        {
            var model = MeNewTransferModel.Create(id, fromClientId, toClientId, assetId, amount, feeClientId, feeSizePercentage, overdraft);
            var resultTask = _newTasksManager.Add(model.Id);

            if (!await _tcpOrderSocketService.SendDataToSocket(model))
            {
                _newTasksManager.Compliete(model.Id, null);
                return null;
            }

            var result = await resultTask;
            return result.ToDomainModel();
        }

        public async Task<MeResponseModel> SwapAsync(string id,
            string clientId1, string assetId1, double amount1,
            string clientId2, string assetId2, double amount2)
        {
            var model = MeNewSwapModel.Create(id,
                clientId1, assetId1, amount1,
                clientId2, assetId2, amount2);
            var resultTask = _newTasksManager.Add(model.Id);

            if (!await _tcpOrderSocketService.SendDataToSocket(model))
                return null;

            var result = await resultTask;
            return result.ToDomainModel();
        }

        public async Task<MeResponseModel> PlaceLimitOrderAsync(LimitOrderModel model)
        {
            var limitOrderModel = model.ToNewMeModel();
            var resultTask = _newTasksManager.Add(limitOrderModel.Id);

            if (!await _tcpOrderSocketService.SendDataToSocket(limitOrderModel))
                return null;

            var result = await resultTask;
            return result.ToDomainModel();
        }

        public async Task<MeResponseModel> CancelLimitOrderAsync(string limitOrderId)
        {
            var model = MeNewLimitOrderCancelModel.Create(Guid.NewGuid().ToString(), limitOrderId);
            var resultTask = _newTasksManager.Add(model.Id);

            if (!await _tcpOrderSocketService.SendDataToSocket(model))
                return null;

            var result = await resultTask;
            return result.ToDomainModel();
        }

        public async Task<string> HandleMarketOrderAsync(string clientId, string assetPairId,
            OrderAction orderAction, double volume, bool straight, double? reservedLimitVolume = null)
        {
            var id = GetNextRequestId();

            var model = MeMarketOrderObsoleteModel.Create(id, clientId,
                assetPairId, orderAction, volume, straight, reservedLimitVolume);
            var resultTask = _tasksManager.Add(model.Id);

            await _tcpOrderSocketService.SendDataToSocket(model);
            var result = await resultTask;

            return result.RecordId;
        }

        public async Task<MarketOrderResponse> HandleMarketOrderAsync(MarketOrderModel model)
        {
            var marketOrderModel = model.ToMeModel();

            var resultTask = _marketOrderTasksManager.Add(marketOrderModel.Id);

            await _tcpOrderSocketService.SendDataToSocket(marketOrderModel);
            var result = await resultTask;

            return new MarketOrderResponse
            {
                Price = result.Price,
                Status = (MeStatusCodes)result.Status
            };
        }

        public void Start()
        {
            _clientTcpSocket.Start();
        }

        public bool IsConnected => _clientTcpSocket.Connected;

        public SocketStatistic SocketStatistic => _clientTcpSocket.SocketStatistic;
    }
}