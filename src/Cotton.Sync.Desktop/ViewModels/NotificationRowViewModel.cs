// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels
{
    internal class NotificationRowViewModel : ViewModelBase
    {
        private bool _isDashboardVisible = true;
        private string _message = string.Empty;
        private string _title = string.Empty;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public bool IsDashboardVisible
        {
            get => _isDashboardVisible;
            set => SetProperty(ref _isDashboardVisible, value);
        }
    }
}
