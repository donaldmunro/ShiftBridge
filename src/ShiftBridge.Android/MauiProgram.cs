using ShiftBridge.Core.Config;
using ShiftBridge.Core.Localization;

namespace ShiftBridge.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // La langue est fixée au démarrage du processus, avant toute page et avant le ViewModel (que le service de premier
        // plan peut aussi créer avant l'activité).
        AppLanguage.Apply(ConfigStore.Load().Language);
        return MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("MaterialIcons-Regular.ttf", Icons.Font)) // icônes de la navigation
            .Build();
    }
}
