﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Blazored.Typeahead.Forms
{
    public class BlazoredTypeaheadInputBase<TItem> : InputBase<TItem>, IDisposable
    {
        [Inject] IJSRuntime JSRuntime { get; set; }

        [Parameter] public string Placeholder { get; set; }
        [Parameter] public Func<string, Task<IEnumerable<TItem>>> SearchMethod { get; set; }
        [Parameter] public RenderFragment NotFoundTemplate { get; set; }
        [Parameter] public RenderFragment<TItem> ResultTemplate { get; set; }
        [Parameter] public RenderFragment<TItem> SelectedTemplate { get; set; }
        [Parameter] public RenderFragment FooterTemplate { get; set; }
        [Parameter] public int MinimumLength { get; set; } = 1;
        [Parameter] public int Debounce { get; set; } = 300;
        [Parameter] public int MaximumSuggestions { get; set; } = 25;

        protected bool Searching { get; set; } = false;
        protected bool ShouldShowMenu { get; private set; } = false;
        protected bool ShouldShowInput { get; private set; } = true;
        protected bool ShouldShowMask { get; private set; } = false;
        protected TItem[] SearchResults { get; set; } = new TItem[0];
        
        private Timer _debounceTimer;
        protected ElementReference searchInput;

        private string _searchText;
        protected string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;

                if (value.Length == 0)
                {
                    _debounceTimer.Stop();
                    SearchResults = new TItem[0];
                }
                else if (value.Length >= MinimumLength)
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
            }
        }

        protected override void OnInitialized()
        {
            if (SearchMethod == null)
            {
                throw new InvalidOperationException($"{GetType()} requires a {nameof(SearchMethod)} parameter.");
            }

            if (SelectedTemplate == null)
            {
                throw new InvalidOperationException($"{GetType()} requires a {nameof(SelectedTemplate)} parameter.");
            }

            if (ResultTemplate == null)
            {
                throw new InvalidOperationException($"{GetType()} requires a {nameof(ResultTemplate)} parameter.");
            }

            _debounceTimer = new Timer();
            _debounceTimer.Interval = Debounce;
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += Search;

            if (Value == null)
            {
                ShouldShowMask = false;
                ShouldShowInput = true;
            }
            else
            {
                ShouldShowInput = true;
                ShouldShowMask = false;
            }
        }

        protected void HandleClick()
        {
            SearchText = "";
            ShouldShowMenu = false;
            ShouldShowMask = false;
            ShouldShowInput = true;
        }
        protected async Task HandleMaskClick()
        {
            SearchText = "";
            ShouldShowInput = true;
            ShouldShowMenu = false;
            ShouldShowMask = false;
            await Task.Delay(250); // Possible race condition here, but it's not harmfull
            await JSRuntime.InvokeAsync<object>("blazoredTypeahead.setFocus", searchInput);
        }

        protected async Task HandleClear()
        {
            await ValueChanged.InvokeAsync(default);
            EditContext.NotifyFieldChanged(FieldIdentifier);

            SearchText = "";
            ShouldShowMenu = false;
            ShouldShowInput = true;
            ShouldShowMask = false;
            await Task.Delay(250); // Possible race condition here, but it's not harmfull
            await JSRuntime.InvokeAsync<object>("blazoredTypeahead.setFocus", searchInput);
        }

        protected async Task ShowMaximumSuggestions()
        {
            ShouldShowMenu = !ShouldShowMenu;

            if (ShouldShowMenu)
            {
                _searchText = "";
                Searching = true;
                await InvokeAsync(StateHasChanged);

                SearchResults = (await SearchMethod?.Invoke(_searchText)).Take(MaximumSuggestions).ToArray();

                Searching = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected async Task HandleKeyUpOnSuggestion(UIKeyboardEventArgs args, TItem item)
        {
            if (args.Key == "Enter")
                await SelectResult(item);
        }

        protected string GetSelectedSuggestionClass(TItem item)
        {
            if (Value == null)
                return null;
            if (Value.Equals(item))
                return "blazored-typeahead__result-selected"; 
            return null;
        }

        protected async void Search(Object source, ElapsedEventArgs e)
        {
            Searching = true;
            await InvokeAsync(StateHasChanged);
            SearchResults = (await SearchMethod?.Invoke(_searchText)).Take(MaximumSuggestions).ToArray();

            Searching = false;
            ShouldShowMenu = true;
            await InvokeAsync(StateHasChanged);
        }

        protected async Task SelectResult(TItem item)
        {
            await ValueChanged.InvokeAsync(item);
            EditContext.NotifyFieldChanged(FieldIdentifier);

            ShouldShowMenu = false;
            ShouldShowInput = false;
            ShouldShowMask = true;
        }

        protected bool ShouldShowSuggestions()
        {
            return ShouldShowMenu &&
                   SearchResults.Any();
        }

        private bool HasValidSearch => !string.IsNullOrWhiteSpace(SearchText) && SearchText.Length >= MinimumLength;

        private bool IsSearchingOrDebouncing => Searching || _debounceTimer.Enabled;

        protected bool ShowNotFound()
        {
            return ShouldShowMenu &&
                   HasValidSearch &&
                   !IsSearchingOrDebouncing &&
                   !SearchResults.Any();
        }

        protected override bool TryParseValueFromString(string value, out TItem result, out string validationErrorMessage)
        {
            result = (TItem)(object)value;
            validationErrorMessage = null;

            return true;
        }

        public void Dispose()
        {
            _debounceTimer.Dispose();
        }

    }
}
