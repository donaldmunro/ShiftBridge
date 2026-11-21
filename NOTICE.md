# Notices

Copyright (C) 2025-2026 Antoine Renou. Ce programme est un logiciel libre : vous pouvez le redistribuer et/ou le modifier selon les termes de la licence GNU GPL v3 (fichier `LICENSE`). Il est distribué sans aucune garantie.

ShiftBridge est distribué sous licence GPL-3.0. Il s'appuie sur des travaux publics de rétro-ingénierie et sur une spécification ouverte :

- **OpenBikeControl protocol** (MIT) — https://github.com/OpenBikeControl/openbikecontrol-protocol
- **qdomyos-zwift** (GPL-3.0) — https://github.com/cagnulein/qdomyos-zwift : format des trames Play (0x07) et Ride (0x23), masques de boutons.
- **BikeControl, versions antérieures au tag `gpl3`** (GPL-3.0) — https://github.com/OpenBikeControl/bikecontrol/tree/gpl3 : UUIDs Zwift, handshake RideOn, types d'appareils. Les versions ultérieures sont sous licence non commerciale : aucun code n'en est repris.
- **Berg0162/DirCon** (GPL-3.0) — https://github.com/Berg0162/DirCon : format des paquets Wahoo Direct Connect (DirCon / WFTNP), identifiants de messages, codes de réponse.
- **Virtual shifting (mise à l'échelle de la résistance et braquet réel)** — la formule de Zwift n'est pas publiée ; ShiftBridge s'appuie sur les principes communs aux implémentations ouvertes, réécrits sans reprise de code : **home-trainer-kit** (MIT, https://github.com/enavarro222/home-trainer-kit : pente ×k, roulement ×k, traînée ×k³, k = rapport virtuel ÷ plateau/pignon saisi), **Serpentine** (MIT, https://github.com/chrisperkles/serpentine : plateau et pignon saisis, rapport 2,4 neutre), **Wattroom** (AGPL-3.0, https://github.com/natrontech/wattroom : mêmes relations, report du surplus de traînée sur la pente, détection du rapport réel par vitesse/cadence avec médiane glissante — algorithme décrit, aucun code copié) et **OpenRadlr** (AGPL-3.0). Table des 24 rapports virtuels (0,75 à 5,49) : Zwift Insider (https://zwiftinsider.com/virtual-shifting/), identique à celle de BikeControl.
- **Mode natif Zwift (protocole des trainers « Zwift Ready »)** — non documenté par Zwift ; réimplémenté d'après les faits de protocole publiés (UUID, handshake, champs protobuf, séquence d'initialisation) par **Makinolo** (« Zwift trainer protocol », https://www.makinolo.com/blog/2024/10/20/zwift-trainer-protocol/), **TrainerHUD** (MIT, https://github.com/hugoBourretDesmarais/TrainerHUD) et **serpentine** (MIT, https://github.com/chrisperkles/serpentine). Aucun code n'est copié ; qdomyos-zwift (GPL-3.0) et vshift n'ont pas été repris.
- **ajchellew/zwiftplay** et l'article de Makinolo sur le Zwift Play : champs protobuf du Play, commande de vibration.
- **Material Icons** (Apache-2.0) — https://github.com/google/material-design-icons : police d'icônes de la navigation de la version Android (`src/ShiftBridge.Android/Resources/Fonts/MaterialIcons-Regular.ttf`, inchangée).

Aucune information de ce projet n'est officielle ni fournie par Zwift Inc. « Zwift » est une marque de Zwift, Inc. ; ce projet n'est ni affilié à Zwift ni approuvé par elle.
