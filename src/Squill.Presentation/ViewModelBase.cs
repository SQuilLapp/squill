using CommunityToolkit.Mvvm.ComponentModel;

namespace Squill.Presentation;

/// <summary>Base class of every view-model; raises <c>PropertyChanged</c> through the MVVM toolkit.</summary>
public abstract class ViewModelBase : ObservableObject;
