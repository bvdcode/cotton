// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels
{
    internal class SelfTestItemRowViewModel : ViewModelBase
    {
        private string _details = string.Empty;
        private string _name = string.Empty;
        private bool _passed;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Details
        {
            get => _details;
            set => SetProperty(ref _details, value);
        }

        public bool Passed
        {
            get => _passed;
            set
            {
                if (SetProperty(ref _passed, value))
                {
                    OnPropertyChanged(nameof(ResultText));
                    OnPropertyChanged(nameof(IsFailed));
                }
            }
        }

        public string ResultText => Passed ? "OK" : "Issue";

        public bool IsFailed => !Passed;
    }
}
