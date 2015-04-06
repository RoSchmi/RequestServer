using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Updater : IDisposable {
		private Task worker;
		private CancellationTokenSource cancellationSource;
		private bool disposed;
		private bool running;

		public Node Node { get; set; }
		public TimeSpan Interval { get; set; }
		public MessageContext Context => this.Node.Context;

		protected Updater() {
			this.disposed = false;
			this.running = false;
		}

		public void Start() {
			if (this.disposed) throw new ObjectDisposedException(nameof(Updater));
			if (this.running) throw new InvalidOperationException("Already started.");
			if (this.Node == null) throw new InvalidOperationException(nameof(this.Node));
			if (this.Interval == default(TimeSpan)) throw new InvalidOperationException(nameof(this.Interval));

			this.running = true;

			this.cancellationSource = new CancellationTokenSource();

			this.worker = new Task(this.DoWork, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public void Shutdown() {
			if (this.disposed) throw new ObjectDisposedException(nameof(Updater));
			if (!this.running) throw new InvalidOperationException("Not started.");

			this.running = false;

			this.cancellationSource.Cancel();

			this.worker.Wait();
			this.worker.Dispose();
			this.worker = null;

			this.cancellationSource.Dispose();
			this.cancellationSource = null;
		}

		private async void DoWork() {
			var next = DateTime.UtcNow;

			while (!this.cancellationSource.IsCancellationRequested) {
				next = next.Add(this.Interval);

				this.Node.OnUpdateStarted();

				this.Context?.BeginMessage();

				this.Tick();

				this.Context?.SaveChanges();

				this.Context?.EndMessage();

				this.Node.OnUpdateFinished();

				try {
					await Task.Delay(next - DateTime.UtcNow, this.cancellationSource.Token);
				}
				catch (TaskCanceledException) { 
					break;
				}
			}
		}

		public abstract void Tick();

		protected virtual void Dispose(bool disposing) {
			if (this.disposed)
				return;

			if (disposing && this.running)
				this.Shutdown();

			this.disposed = true;
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}

	public abstract class Updater<T> : Updater where T : MessageContext {
		public new T Context => (T)base.Context;
	}
}