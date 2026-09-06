namespace PumpYaqobi.App.ViewModels;

/// <summary>بخشی که هنوز ساخته نشده — تا وقتی که نوبتش برسد.</summary>
public sealed class PlaceholderSectionViewModel : SectionViewModel
{
    public PlaceholderSectionViewModel(string id, string title) : base(id, id, title) { }
}
