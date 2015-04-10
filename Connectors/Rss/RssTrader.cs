namespace StockSharp.Rss
{
	using System;

	using Ecng.Common;

	using StockSharp.Algo;
	using StockSharp.BusinessEntities;
	using StockSharp.Messages;
	using StockSharp.Localization;

	/// <summary>
	/// Реализация интерфейса <see cref="IConnector"/> для взаимодействия с RSS фидами.
	/// </summary>
	public class RssTrader : Connector
    {
		private sealed class RssTransactionMessageAdapter : MessageAdapter
		{
			public RssTransactionMessageAdapter(IdGenerator transactionIdGenerator)
				: base(transactionIdGenerator)
			{
			}

			public override bool IsMarketDataEnabled
			{
				get { return false; }
			}

			public override bool IsTransactionEnabled
			{
				get { return true; }
			}

			protected override void OnSendInMessage(Message message)
			{
				switch (message.Type)
				{
					case MessageTypes.Connect:
						SendOutMessage(new ConnectMessage());
						break;

					case MessageTypes.Disconnect:
						SendOutMessage(new DisconnectMessage());
						break;

					case MessageTypes.Time: // обработка heartbeat
						break;

					default:
						throw new NotSupportedException(LocalizedStrings.Str2143Params.Put(message.Type));
				}
			}
		}

		private readonly RssMarketDataMessageAdapter _adapter;

		/// <summary>
		/// Создать <see cref="RssTrader"/>.
		/// </summary>
		public RssTrader()
		{
			TransactionAdapter = new RssTransactionMessageAdapter(TransactionIdGenerator);
			
			_adapter = new RssMarketDataMessageAdapter(TransactionIdGenerator);
			MarketDataAdapter = _adapter.ToChannel(this);
		}

		/// <summary>
		/// Адрес RSS фида.
		/// </summary>
		public Uri Address
		{
			get { return _adapter.Address; }
			set { _adapter.Address = value; }
		}

		/// <summary>
		/// Формат дат. Необходимо заполнить, если формат RSS потока отличается от ddd, dd MMM yyyy hh:mm:ss.
		/// </summary>
		public string CustomDateFormat
		{
			get { return _adapter.CustomDateFormat; }
			set { _adapter.CustomDateFormat = value; }
		}
    }
}