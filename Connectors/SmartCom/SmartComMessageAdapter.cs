namespace StockSharp.SmartCom
{
	using System;
	using System.Collections.Generic;

	using Ecng.Common;
	using Ecng.Interop;

	using StockSharp.BusinessEntities;
	using StockSharp.Messages;
	using StockSharp.SmartCom.Native;
	using StockSharp.Localization;

	/// <summary>
	/// Адаптер сообщений для SmartCOM.
	/// </summary>
	public partial class SmartComMessageAdapter : MessageAdapter
	{
		private ISmartComWrapper _wrapper;

		/// <summary>
		/// Создать <see cref="SmartComMessageAdapter"/>.
		/// </summary>
		/// <param name="transactionIdGenerator">Генератор идентификаторов транзакций.</param>
		public SmartComMessageAdapter(IdGenerator transactionIdGenerator)
			: base(transactionIdGenerator)
		{
			Version = SmartComVersions.V3;

			SecurityClassInfo.Add("OPT", RefTuple.Create(SecurityTypes.Option, ExchangeBoard.Forts.Code));
			SecurityClassInfo.Add("OPTM", RefTuple.Create(SecurityTypes.Option, ExchangeBoard.Forts.Code));
			SecurityClassInfo.Add("FUT", RefTuple.Create(SecurityTypes.Future, ExchangeBoard.Forts.Code));

			PortfolioBoardCodes = new Dictionary<string, string>
			{
			    { "EQ", ExchangeBoard.MicexEqbr.Code },
			    { "FOB", ExchangeBoard.MicexFbcb.Code },
			    { "RTS_FUT", ExchangeBoard.Forts.Code },
			};

			UpdatePlatform();
		}

		/// <summary>
		/// <see langword="true"/>, если сессия используется для получения маркет-данных, иначе, <see langword="false"/>.
		/// </summary>
		public override bool IsMarketDataEnabled
		{
			get { return true; }
		}

		/// <summary>
		/// <see langword="true"/>, если сессия используется для отправки транзакций, иначе, <see langword="false"/>.
		/// </summary>
		public override bool IsTransactionEnabled
		{
			get { return true; }
		}

		/// <summary>
		/// Создать для заявки типа <see cref="OrderTypes.Conditional"/> условие, которое поддерживается подключением.
		/// </summary>
		/// <returns>Условие для заявки. Если подключение не поддерживает заявки типа <see cref="OrderTypes.Conditional"/>, то будет возвращено null.</returns>
		public override OrderCondition CreateOrderCondition()
		{
			return new SmartComOrderCondition();
		}

		private void UpdatePlatform()
		{
			Platform = Version == SmartComVersions.V3 ? Platforms.AnyCPU : Platforms.x86;
		}

		/// <summary>
		/// Требуется ли дополнительное сообщение <see cref="PortfolioLookupMessage"/> для получения списка портфелей и позиций.
		/// </summary>
		public override bool PortfolioLookupRequired
		{
			get { return true; }
		}

		/// <summary>
		/// Требуется ли дополнительное сообщение <see cref="SecurityLookupMessage"/> для получения списка инструментов.
		/// </summary>
		public override bool SecurityLookupRequired
		{
			get { return true; }
		}

		/// <summary>
		/// Отправить сообщение.
		/// </summary>
		/// <param name="message">Сообщение.</param>
		protected override void OnSendInMessage(Message message)
		{
			switch (message.Type)
			{
				case MessageTypes.Connect:
				{
					if (_wrapper != null)
						throw new InvalidOperationException(LocalizedStrings.Str1619);

					_tempDepths.Clear();
					_candleTransactions.Clear();
					_bestQuotes.Clear();

					_lookupSecuritiesId = 0;
					_lookupPortfoliosId = 0;

					//_smartOrderIds.Clear();
					//_smartIdOrders.Clear();

					switch (Version)
					{
						case SmartComVersions.V2:
							_wrapper = new SmartCom2Wrapper();
							break;
						case SmartComVersions.V3:
							_wrapper = (Environment.Is64BitProcess
								? (ISmartComWrapper)new SmartCom3Wrapper64
								{
									ClientSettings = ClientSettings,
									ServerSettings = ServerSettings,
								}
								: new SmartCom3Wrapper32
								{
									ClientSettings = ClientSettings,
									ServerSettings = ServerSettings,
								});

							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					_wrapper.NewPortfolio += OnNewPortfolio;
					_wrapper.PortfolioChanged += OnPortfolioChanged;
					_wrapper.PositionChanged += OnPositionChanged;
					_wrapper.NewMyTrade += OnNewMyTrade;
					_wrapper.NewOrder += OnNewOrder;
					_wrapper.OrderFailed += OnOrderFailed;
					_wrapper.OrderCancelFailed += OnOrderCancelFailed;
					_wrapper.OrderChanged += OnOrderChanged;
					_wrapper.OrderReRegisterFailed += OnOrderReRegisterFailed;
					_wrapper.OrderReRegistered += OnOrderReRegistered;

					_wrapper.NewSecurity += OnNewSecurity;
					_wrapper.SecurityChanged += OnSecurityChanged;
					_wrapper.QuoteChanged += OnQuoteChanged;
					_wrapper.NewTrade += OnNewTrade;
					_wrapper.NewHistoryTrade += OnNewHistoryTrade;
					_wrapper.NewBar += OnNewBar;

					_wrapper.Connected += OnConnected;
					_wrapper.Disconnected += OnDisconnected;

					_wrapper.Connect(Address.GetHost(), (short)Address.GetPort(), Login, Password.To<string>());

					break;
				}

				case MessageTypes.Disconnect:
				{
					if (_wrapper == null)
						throw new InvalidOperationException(LocalizedStrings.Str1856);

					_wrapper.Disconnect();

					break;
				}

				case MessageTypes.OrderRegister:
					ProcessRegisterMessage((OrderRegisterMessage)message);
					break;

				case MessageTypes.OrderCancel:
					ProcessCancelMessage((OrderCancelMessage)message);
					break;

				case MessageTypes.OrderGroupCancel:
					_wrapper.CancelAllOrders();
					break;

				case MessageTypes.OrderReplace:
					ProcessReplaceMessage((OrderReplaceMessage)message);
					break;

				case MessageTypes.Portfolio:
					ProcessPortfolioMessage((PortfolioMessage)message);
					break;

				case MessageTypes.PortfolioLookup:
					ProcessPortolioLookupMessage((PortfolioLookupMessage)message);
					break;

				case MessageTypes.MarketData:
					ProcessMarketDataMessage((MarketDataMessage)message);
					break;

				case MessageTypes.SecurityLookup:
					ProcessSecurityLookupMessage((SecurityLookupMessage)message);
					break;
			}
		}

		private void OnConnected()
		{
			SendOutMessage(new ConnectMessage());
		}

		private void OnDisconnected(Exception error)
		{
			_wrapper.NewPortfolio -= OnNewPortfolio;
			_wrapper.PortfolioChanged -= OnPortfolioChanged;
			_wrapper.PositionChanged -= OnPositionChanged;
			_wrapper.NewMyTrade -= OnNewMyTrade;
			_wrapper.NewOrder -= OnNewOrder;
			_wrapper.OrderFailed -= OnOrderFailed;
			_wrapper.OrderCancelFailed -= OnOrderCancelFailed;
			_wrapper.OrderChanged -= OnOrderChanged;
			_wrapper.OrderReRegisterFailed -= OnOrderReRegisterFailed;
			_wrapper.OrderReRegistered -= OnOrderReRegistered;

			_wrapper.NewSecurity -= OnNewSecurity;
			_wrapper.SecurityChanged -= OnSecurityChanged;
			_wrapper.QuoteChanged -= OnQuoteChanged;
			_wrapper.NewTrade -= OnNewTrade;
			_wrapper.NewHistoryTrade -= OnNewHistoryTrade;
			_wrapper.NewBar -= OnNewBar;

			_wrapper.Connected -= OnConnected;
			_wrapper.Disconnected -= OnDisconnected;

			SendOutMessage(new DisconnectMessage { Error = error });

			_wrapper = null;
		}
	}
}