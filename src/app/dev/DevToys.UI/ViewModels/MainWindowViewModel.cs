﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DevToys.Core.Tools;
using DevToys.Core.Tools.ViewItems;
using DevToys.UI.Models;
using Microsoft.UI.Xaml.Controls;

namespace DevToys.UI.ViewModels;

[Export]
internal sealed partial class MainWindowViewModel : ObservableRecipient
{
    private readonly GuiToolProvider _guiToolProvider;

    private INotifyPropertyChanged? _selectedMenuItem;

    [ImportingConstructor]
    public MainWindowViewModel(GuiToolProvider guiToolProvider)
    {
        _guiToolProvider = guiToolProvider;
        Messenger.Register<MainWindowViewModel, ChangeSelectedMenuItemMessage>(this, OnChangeSelectedMenuItemMessageReceived);
    }

    /// <summary>
    /// Raised when the <see cref="SelectedMenuItem"/> property changed.
    /// </summary>
    internal event EventHandler<EventArgs>? SelectedMenuItemChanged;

    /// <summary>
    /// Gets a hierarchical list containing all the tools available, ordered, to display in the top and body menu.
    /// This includes "All tools" menu item, recents and favorites.
    /// </summary>
    internal ReadOnlyObservableCollection<INotifyPropertyChanged> HeaderAndBodyToolViewItems => _guiToolProvider.HeaderAndBodyToolViewItems;

    /// <summary>
    /// Gets a flat list containing all the footer tools available, ordered.
    /// </summary>
    internal ReadOnlyObservableCollection<GuiToolViewItem> FooterToolViewItems => _guiToolProvider.FooterToolViewItems;

    // Can't use CommunityToolkit.MVVM due to https://github.com/dotnet/roslyn/issues/57239#issuecomment-1437895948
    /// <summary>
    /// Gets or sets the selected menu item in the NavitationView.
    /// </summary>
    internal INotifyPropertyChanged? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            if (value is null && _selectedMenuItem is not null)
            {
                // Somehow, the NavigationView end up with no selected item. An example of scenario where it can happen is when
                // the selected item is a favorite one and the user remove it from the favorites.
                // What we do in this scenario is that we try to find the equivalent item corresponding to the selected item we had.
                // If we don't find anything, let's selected the first item in the menu.
                SetProperty(ref _selectedMenuItem, GetBestMenuItemToSelect(_selectedMenuItem));
            }
            else
            {
                SetProperty(ref _selectedMenuItem, value);
            }

            OnPropertyChanged(nameof(IsSelectedMenuItemATool));
            SelectedMenuItemChanged?.Invoke(this, EventArgs.Empty);

            if (value is GuiToolViewItem guiToolViewItem)
            {
                guiToolViewItem.RaiseGotSelected();
            }
        }
    }

    /// <summary>
    /// Indicates whether the <see cref="SelectedMenuItem"/> is a tool or not.
    /// </summary>
    internal bool IsSelectedMenuItemATool => SelectedMenuItem is GuiToolViewItem;

    // Can't use CommunityToolkit.MVVM due to https://github.com/dotnet/roslyn/issues/57239#issuecomment-1437895948
    /// <summary>
    /// Gets or sets the search query typed by the user.
    /// </summary>
    internal string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of search results from the <see cref="SearchQuery"/>.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<GuiToolViewItem> _searchResults = new();

    [RelayCommand]
    private void SearchBoxTextChanged(AutoSuggestBoxTextChangedEventArgs parameters)
    {
        // Since selecting an item will also change the text,
        // only listen to changes caused by user entering text.
        if (parameters.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            _guiToolProvider.SearchTools(SearchQuery, SearchResults);
        }
    }

    [RelayCommand]
    private void SearchBoxQuerySubmitted(AutoSuggestBoxQuerySubmittedEventArgs parameters)
    {
        var selectedSearchResultItem = parameters.ChosenSuggestion as GuiToolViewItem;
        if (selectedSearchResultItem is null && SearchResults.Count > 0)
        {
            selectedSearchResultItem = SearchResults[0];
        }

        if (selectedSearchResultItem is null || selectedSearchResultItem == GuiToolProvider.NoResultFoundItem)
        {
            return;
        }

        // Select the actual menu item in the navigation view. This will trigger the navigation.
        SelectedMenuItem = GetBestMenuItemToSelect(selectedSearchResultItem);
    }

    private INotifyPropertyChanged GetBestMenuItemToSelect(object currentSelectedMenuItem)
    {
        Guard.IsNotEmpty((IReadOnlyList<INotifyPropertyChanged>)HeaderAndBodyToolViewItems);
        Guard.IsNotNull(currentSelectedMenuItem);

        GuiToolInstance? guiToolInstance = null;
        if (currentSelectedMenuItem is GuiToolViewItem guiToolViewItem)
        {
            guiToolInstance = guiToolViewItem.ToolInstance;
        }
        else if (currentSelectedMenuItem is GuiToolInstance instance)
        {
            guiToolInstance = instance;
        }

        if (guiToolInstance is not null)
        {
            GuiToolViewItem? itemToSelect = _guiToolProvider.GetViewItemFromTool(guiToolInstance).FirstOrDefault();
            if (itemToSelect is not null)
            {
                return itemToSelect;
            }
        }

        return HeaderAndBodyToolViewItems[0];
    }

    private void OnChangeSelectedMenuItemMessageReceived(MainWindowViewModel vm, ChangeSelectedMenuItemMessage message)
    {
        // Select the actual menu item in the navigation view. This will trigger the navigation.
        SelectedMenuItem = GetBestMenuItemToSelect(message.Value);
    }
}
