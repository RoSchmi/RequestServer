using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Updater<TContext> where TContext : MessageContext {
		private bool running;
		private Task worker;
		private Node<TContext> node;
		private CancellationTokenSource sleepCanceller;

		public TimeSpan Interval { get; set; }

		protected TContext Context { get { return this.node.Context; } }

		public Updater(Node<TContext> node) {
			this.node = node;
			this.running = false;
		}

		public void Start() {
			this.running = true;
			this.sleepCanceller = new CancellationTokenSource();

			this.worker = new Task(this.DoWork, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public void Stop() {
			this.running = false;
			this.sleepCanceller.Cancel();

			this.worker.Wait();
		}

		private async void DoWork() {
			var next = DateTime.UtcNow;

			while (this.running) {
				next = next.Add(this.Interval);

				this.node.OnUpdateStarted();

				this.node.Context.BeginMessage();

				this.Tick();

				this.node.Context.SaveChanges();

				this.node.Context.EndMessage();

				this.node.OnUpdateFinished();

				try {
					await Task.Delay(next - DateTime.UtcNow, this.sleepCanceller.Token);
				}
				catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerExceptions[0] is TaskCanceledException) {
					break;
				}
			}
		}

		public abstract void Tick();
	}
}