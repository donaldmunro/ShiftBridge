namespace ShiftBridge.Presentation;

/// <summary>Marshalisation vers le thread d'interface (Dispatcher WPF, MainThread MAUI…).</summary>
public interface IUiDispatcher
{
    /// <summary>Exécute l'action sur le thread UI : tout de suite si on y est déjà, sinon en différé. Sans UI (arrêt), ne fait rien.</summary>
    void Post(Action action);
}

/// <summary>Services de l'application hôte dont le ViewModel a besoin, propres à chaque interface.</summary>
public interface IAppHost
{
    /// <summary>Version affichée (p. ex. « 0.7.2 »).</summary>
    string AppVersion { get; }

    /// <summary>Copie un texte dans le presse-papiers ; ne lève jamais d'exception (presse-papiers occupé…).</summary>
    void CopyText(string text);

    /// <summary>Ouvre les réglages système de l'application (permissions). Sans effet là où cela n'a pas de sens (Windows).</summary>
    void OpenAppSettings() { }

    /// <summary>Ouvre les réglages d'optimisation de la batterie (Android : sans exclusion, le système peut tuer le pont).</summary>
    void OpenBatterySettings() { }

    /// <summary>Bluetooth allumé ? Toujours vrai là où l'hôte ne peut pas le savoir (Windows).</summary>
    bool IsBluetoothOn => true;

    /// <summary>Affiche la demande système d'activation du Bluetooth. La reprise passe par <c>MainViewModel.OnBluetoothTurnedOn</c>.</summary>
    void RequestBluetoothOn() { }

    /// <summary>Appareil connecté à un réseau local (Wi-Fi ou Ethernet) ? Toujours vrai là où l'hôte ne peut pas le savoir.</summary>
    bool IsWifiOn => true;

    /// <summary>Ouvre le panneau Wi-Fi (Android ne permet plus à une appli de l'activer elle-même).</summary>
    void RequestWifiOn() { }

    /// <summary>Pose une question oui/non à l'utilisateur. Vrai par défaut là où l'hôte n'a pas de boîte de dialogue.</summary>
    bool Confirm(string message) => true;

    /// <summary>Mise à jour intégrée ; null là où l'application est mise à jour par une boutique (Android).</summary>
    IAppUpdater? Updater => null;

    /// <summary>Ouvre une adresse https dans le navigateur par défaut ; ne lève jamais d'exception.</summary>
    void OpenUrl(string url) { }
}
