// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Windows.Input;

namespace Cotton.Sync.Desktop.ViewModels
{
    /// <summary>
    /// Runs an asynchronous view-model command.
    /// </summary>
    internal class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private readonly Action<Exception>? _onError;
        private bool _isRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRelayCommand" /> class.
        /// </summary>
        public AsyncRelayCommand(
            Func<Task> execute,
            Func<bool>? canExecute = null,
            Action<Exception>? onError = null)
            : this(
                _ => execute(),
                canExecute is null ? null : _ => canExecute(),
                onError)
        {
        }

        public AsyncRelayCommand(
            Func<object?, Task> execute,
            Func<object?, bool>? canExecute = null,
            Action<Exception>? onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Gets a value indicating whether the command is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <inheritdoc />
        public bool CanExecute(object? parameter)
        {
            return !_isRunning && (_canExecute?.Invoke(parameter) ?? true);
        }

        /// <inheritdoc />
        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isRunning = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _onError?.Invoke(exception);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
