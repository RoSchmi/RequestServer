using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Updater<ContextType> where ContextType : MessageContext {
		private Task worker;

		public CancellationToken CancellationToken { get; set; }
		public TimeSpan Interval { get; set; }
		public DateTime LastRun { get; set; }
		public ContextType Context { get; set; }

		public void Start() {
			this.worker = new Task(this.DoWork, this.CancellationToken, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public void Stop() {
			this.worker.Wait();
		}

		private void DoWork() {
			while (!this.CancellationToken.IsCancellationRequested) {
				var now = DateTime.UtcNow;

				try {
					Task.Delay(this.Interval - (now - this.LastRun), this.CancellationToken).Wait();
				}
				catch (AggregateException e) if (e.InnerExceptions.Count == 1 && e.InnerExceptions[0] is TaskCanceledException) {
					break;
				}

				this.Context.BeginMessage();

				this.Tick(now, (ulong)((now - this.LastRun).TotalMilliseconds));

				this.Context.SaveChanges();

				this.Context.EndMessage();

				this.LastRun = now;
			}
		}

		public abstract void Tick(DateTime now, ulong millisecondsDelta);
	}
}