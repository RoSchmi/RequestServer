using System;
using System.IO;

namespace ArkeIndustries.RequestServer {
    public interface IMessageProvider {
        long HeaderLength { get; }

        INotification CreateNotification(long type, long objectId);
        IRequest CreateRequest(Connection connection);
        IResponse CreateResponse();
    }

    public interface IRequest : IDisposable {
        Stream Header { get; }
        Stream Body { get; }

        Connection Connection { get; }
        long ProcessAttempts { get; set; }

        long BodyLength { get; }
        long TransactionId { get; }
        long RequestId { get; }

        void DeserializeHeader();
    }

    public interface IResponse : IDisposable {
        Stream Header { get; }
        Stream Body { get; }

        Connection Connection { get; set; }
        long SendAttempts { get; set; }

        long BodyLength { get; set; }
        long ResponseCode { get; set; }
        long TransactionId { get; set; }

        void SerializeHeader();
    }

    public interface INotification : IDisposable {
        Stream Header { get; }

        void SerializeHeader();
    }
}
