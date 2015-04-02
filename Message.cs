using System;
using System.IO;

namespace ArkeIndustries.RequestServer {
	public interface IMessageFormat {
		long HeaderLength { get; }

		IMessage CreateNotification(long type, long objectId);
		IMessage CreateMessage();
		IMessage CreateMessage(Connection connection, MemoryStream header);
	}

	public interface IMessage {
		MemoryStream Header { get; }
		MemoryStream Body { get; }

		Connection Connection { get; set; }
		long SendAttempts { get; set; }
		long ProcessAttempts { get; set; }

		long BodyLength { get; set; }
		long ResponseCode { get; set; }
		long RequestId { get; set; }

		void SerializeHeader();
		void DeserializeHeader();
	}
}