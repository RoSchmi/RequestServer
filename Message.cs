using System.IO;

namespace ArkeIndustries.RequestServer {
	public interface IMessageProvider {
		long HeaderLength { get; }

		INotification CreateNotification(long type, long objectId);
		IRequest CreateRequest(Connection connection);
		IResponse CreateResponse();
	}

	public interface IRequest {
		MemoryStream Header { get; }
		MemoryStream Body { get; }

		Connection Connection { get; }
		long ProcessAttempts { get; set; }

		long BodyLength { get; }
		long TransactionId { get; }
		long RequestId { get; }

		void DeserializeHeader();
	}

	public interface IResponse {
		MemoryStream Header { get; }
		MemoryStream Body { get; }

		Connection Connection { get; set; }
		long SendAttempts { get; set; }

		long BodyLength { get; set; }
		long ResponseCode { get; set; }
		long TransactionId { get; set; }

		void SerializeHeader();
	}

	public interface INotification {
		MemoryStream Header { get; }

		void SerializeHeader();
	}
}