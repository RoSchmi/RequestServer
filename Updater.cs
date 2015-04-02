using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Updater {
		private Task worker;
		private CancellationTokenSource cancellationSource;

		public Node Node { get; set; }
		public TimeSpan Interval { get; set; }
		public MessageContext Context => this.Node.Context;

		public void Start() {
			this.cancellationSource = new CancellationTokenSource();

			this.worker = new Task(this.DoWork, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public void Stop() {
			this.cancellationSource.Cancel();

			this.worker.Wait();
		}

		private async void DoWork() {
			var next = DateTime.UtcNow;

			while (!this.cancellationSource.IsCancellationRequested) {
				next = next.Add(this.Interval);

				this.Node.OnUpdateStarted();

				this.Context.BeginMessage();

				this.Tick();

				this.Context.SaveChanges();

				this.Context.EndMessage();

				this.Node.OnUpdateFinished();

				try {
					await Task.Delay(next - DateTime.UtcNow, this.cancellationSource.Token);
				}
				catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerExceptions[0] is TaskCanceledException) {
					break;
				}
			}
		}

		public abstract void Tick();
	}

	public abstract class Updater<T> : Updater where T : MessageContext {
		public new T Context => (T)base.Context;
	}
}