﻿#if NETCOREAPP3_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Extensions.Polling
{
    /// <summary>
    /// Supports asynchronous iteration over <see cref="Update"/>s.
    /// <para>Updates are received on a different thread and enqueued. <see cref="YieldUpdatesAsync"/> yields updates one by one</para>
    /// </summary>
    public class QueuedUpdateReceiver : IYieldingUpdateReceiver
    {
        private static readonly Update[] EmptyUpdates = { };

        /// <summary>
        /// The <see cref="ITelegramBotClient"/> used for making GetUpdates calls
        /// </summary>
        public readonly ITelegramBotClient BotClient;

        /// <summary>
        /// Constructs a new <see cref="QueuedUpdateReceiver"/> for the specified <see cref="ITelegramBotClient"/>
        /// </summary>
        /// <param name="botClient">The <see cref="ITelegramBotClient"/> used for making GetUpdates calls</param>
        public QueuedUpdateReceiver(ITelegramBotClient botClient)
        {
            BotClient = botClient;
        }

        /// <summary>
        /// Indicates whether <see cref="Update"/>s are being received.
        /// <para>Controlled by StartReceiving and StopReceiving</para>
        /// </summary>
        public bool IsReceiving { get; private set; }

        private readonly object _lock = new object();
        private CancellationTokenSource? _cancellationTokenSource;
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _consumerQueueIndex = 0;
        private int _consumerQueueInnerIndex = 0;
        private List<Update[]?> _consumerQueue = new List<Update[]?>(16);
        private List<Update[]?> _producerQueue = new List<Update[]?>(16);
        private int _messageOffset;
        private int _pendingUpdates;

        /// <summary>
        /// Indicates how many <see cref="Update"/>s are ready to be returned by <see cref="YieldUpdatesAsync"/>
        /// </summary>
        public int PendingUpdates => _pendingUpdates;

        /// <summary>
        /// Starts receiving <see cref="Update"/>s on the ThreadPool
        /// </summary>
        /// <param name="allowedUpdates">Indicates which <see cref="UpdateType"/>s are allowed to be received. null means all updates</param>
        /// <param name="errorHandler">The function used to handle <see cref="Exception"/>s</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> with which you can stop receiving</param>
        public void StartReceiving(
            UpdateType[]? allowedUpdates = default,
            Func<Exception, CancellationToken, Task>? errorHandler = default,
            CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (IsReceiving)
                    throw new InvalidOperationException("Receiving is already in progress");

                if (cancellationToken.IsCancellationRequested)
                    return;

                IsReceiving = true;

                _cancellationTokenSource = new CancellationTokenSource();
                cancellationToken.Register(() => _cancellationTokenSource?.Cancel());
                cancellationToken = _cancellationTokenSource.Token;
            }

            StartReceivingInternal(allowedUpdates, errorHandler, cancellationToken);
        }

        private void StartReceivingInternal(
            UpdateType[]? allowedUpdates = default,
            Func<Exception, CancellationToken, Task>? errorHandler = default,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(IsReceiving);
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int timeout = (int)BotClient.Timeout.TotalSeconds;
                    var updates = EmptyUpdates;
                    try
                    {
                        updates = await BotClient.GetUpdatesAsync(
                            offset: _messageOffset,
                            timeout: timeout,
                            allowedUpdates: allowedUpdates,
                            cancellationToken: cancellationToken
                        ).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore
                    }
                    catch (Exception ex)
                    {
                        if (errorHandler != null)
                        {
                            // If the error handler throws then the consumer of this IAsyncEnumerable will wait forever
                            await errorHandler(ex, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (updates.Length > 0)
                    {
                        Interlocked.Add(ref _pendingUpdates, updates.Length);
                        _messageOffset = updates[^1].Id + 1;

                        lock (_lock)
                        {
                            _producerQueue.Add(updates);
                            _tcs.TrySetResult(true);
                        }
                    }
                }

                lock (_lock)
                {
                    Debug.Assert(_cancellationTokenSource != null);
                    Debug.Assert(IsReceiving);
                    _cancellationTokenSource = null;
                    IsReceiving = false;

                    // Signal the TCS so that we can stop the yielding loop
                    _tcs.TrySetResult(true);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Stops receiving <see cref="Update"/>s.
        /// <para><see cref="YieldUpdatesAsync"/> will continue to yield <see cref="Update"/>s as long as <see cref="PendingUpdates"/> are available</para>
        /// </summary>
        public void StopReceiving()
        {
            lock (_lock)
            {
                if (!IsReceiving || _cancellationTokenSource == null)
                    return;

                _cancellationTokenSource.Cancel();
            }

            // IsReceiving is set to false by the receiver
        }

        /// <summary>
        /// Yields <see cref="Update"/>s as they are received (or inside <see cref="PendingUpdates"/>).
        /// <para>Call StartReceiving before using this <see cref="IAsyncEnumerable{T}"/>.</para>
        /// <para>This <see cref="IAsyncEnumerable{T}"/> will continue to yield <see cref="Update"/>s
        /// as long as <see cref="IsReceiving"/> is set or there are <see cref="PendingUpdates"/></para>
        /// </summary>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="Update"/></returns>
        public async IAsyncEnumerable<Update> YieldUpdatesAsync()
        {
            // Access to YieldUpdatesAsync is not thread-safe!

            while (true)
            {
                while (_consumerQueueIndex < _consumerQueue.Count)
                {
                    Debug.Assert(_consumerQueue[_consumerQueueIndex] != null);
                    Update[] updateArray = _consumerQueue[_consumerQueueIndex]!;

                    while (_consumerQueueInnerIndex < updateArray.Length)
                    {
                        Interlocked.Decrement(ref _pendingUpdates);

                        // It is vital that we increment before yielding
                        _consumerQueueInnerIndex++;
                        yield return updateArray[_consumerQueueInnerIndex - 1];
                    }

                    // We have reached the end of the current Update[]
                    Debug.Assert(_consumerQueue[_consumerQueueIndex] != null);
                    Debug.Assert(_consumerQueueInnerIndex == _consumerQueue[_consumerQueueIndex]!.Length);

                    // Let the GC collect the Update[], this amortizes the cost of GC on queue swaps
                    _consumerQueue[_consumerQueueIndex] = null;

                    _consumerQueueIndex++;
                    _consumerQueueInnerIndex = 0;
                }

                Debug.Assert(_consumerQueueIndex == _consumerQueue.Count);
                Debug.Assert(_consumerQueueInnerIndex == 0);

                _consumerQueueIndex = 0;

                // We still have to clear all the null references
                Debug.Assert(_consumerQueue.TrueForAll(updateArray => updateArray == null));
                _consumerQueue.Clear();

                // now wait for new updates
                // (no need for await if it's obvious we already have updates pending)
                if (_producerQueue.Count == 0)
                {
                    // If there are no obvious updates and IsReceiving is false, yield break
                    if (!IsReceiving)
                        yield break;

                    await _tcs.Task.ConfigureAwait(false);
                }

                lock (_lock)
                {
                    // We either:
                    // a) have updates in the producer queue
                    // b) the producer queue is empty because StopReceiving was called

                    if (_producerQueue.Count == 0 && !IsReceiving)
                        yield break;

                    // Swap
                    var temp = _producerQueue;
                    _producerQueue = _consumerQueue;
                    _consumerQueue = temp;

                    // Reset the TCS
                    _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
    }
}
#endif