namespace ShiftBridge.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = AppRuntime.ViewModel;
    }

    /// <summary>Le bouton « retour » d'Android remonte d'un niveau (Diagnostic → Réglages → Accueil) avant de quitter l'application.</summary>
    protected override bool OnBackButtonPressed() => AppRuntime.ViewModel.GoBack() || base.OnBackButtonPressed();
}
