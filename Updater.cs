using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Updater<TContext> where TContext : MessageContext {
		private Task worker;
		private Node<TContext> node;

		public CancellationToken CancellationToken { get; set; }
		public TimeSpan Interval { get; set; }

		protected TContext Context { get { return this.node.Context; } }

		public Updater(Node<TContext> node) {
			this.node = node;
		}

		public void Start() {
			this.worker = new Task(this.DoWork, this.CancellationToken, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public void Stop() {
			this.worker.Wait();
		}

		private void DoWork() {
			var next = DateTime.UtcNow;

			while (!this.CancellationToken.IsCancellationRequested) {
				next = next.Add(this.Interval);

				this.node.OnUpdateStarted();

				this.node.Context.BeginMessage();

				this.Tick();

				this.node.Context.SaveChanges();

				this.node.Context.EndMessage();

				this.node.OnUpdateFinished();

				try {
					Task.Delay(next - DateTime.UtcNow, this.CancellationToken).Wait();
				}
				catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerExceptions[0] is TaskCanceledException) {
					break;
				}
			}
		}

		public abstract void Tick();
	}
}