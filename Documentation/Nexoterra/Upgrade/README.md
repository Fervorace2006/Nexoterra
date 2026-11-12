# Prefabs y presentación de Nexoterra

## Edición compartida

- `Assets/02_Prefabs/Gameplay/Player.prefab`: vida, velocidades, habilidades, modelos, arma y retratos del jugador.
- `Assets/02_Prefabs/Gameplay/Enemies/`: Enemy1, Enemy2, Secuaz_Sir_Black, SirBlack y Sir_Black_2. Editar `EnemyCombatant.maxHealth` y los atributos de `EnemyShooter` aquí.
- `Assets/02_Prefabs/Gameplay/Slugs/`: modelos normales y transformados. El componente `SlugPrefabStats` del modelo normal controla el daño, los efectos y los saltos de esa especie.
- `Assets/02_Prefabs/Gameplay/WildSlugs/`: prefabs de población con modelos normales anidados.
- `Assets/02_Prefabs/Gameplay/Models/`: modelos de personajes y materiales.

La campaña y la arena usan instancias conectadas. Las posiciones, nombres, carga de babosas y papeles de cada encuentro son específicos de la escena. La vida ya no tiene overrides individuales en las seis escenas. Se conservaron los valores editados mayoritarios: Enemy1 100, Enemy2 150, secuaz 200, Sir Black 1 250 y Sir Black 2 500. Los cambios deben guardarse fuera de Play.

## Presentación y juego

Siete retratos ilustrados basados en capturas de los modelos están en `Assets/Resources/Nexoterra/UI/`. El menú principal y la pausa usan el logo proporcionado. El HUD mantiene la tarjeta del jugador; los enemigos en combate aparecen arriba a la derecha con retrato, vida numérica y segmentos rojos, sin barras sobre sus cabezas.

Los cuerpos se ocultan después de ocho segundos; se conservan los componentes necesarios para la progresión. Las babosas liberadas sobreviven y recorren el bioma. Energía no tiene versión malvada y se obtiene limpia del primer secuaz. Infierno es única, pero no elemental ni parte de la población salvaje.

Ctrl + W/A/S/D realiza volteretas en cuatro direcciones. El clip frontal normalizado `RollForward_Grounded.anim` elimina la traslación vertical incorrecta de la animación importada. La voltereta hacia atrás reproduce ese movimiento en sentido inverso.

Se recuperaron las estructuras de Cuatro Nodos. El santuario acuático tiene un islote ampliado con colisión sólida y acceso continuo; el volcán tiene un cráter ampliado y acceso recto. Se reconstruyó su navegación para las babosas.

El regreso adapta la duración del salto a la distancia y la velocidad del dueño, actualiza el destino móvil y permite recuperar la babosa sin que el propio collider del personaje la bloquee. Conserva las comprobaciones de obstáculos del mapa.

## Verificación

`Audit.txt`: vínculos y valores serializados de las escenas.
`Library/CampaignEnemyValidation.result`: prueba completa de los seis niveles, inventario enemigo, regreso, recompensas y doble batalla.
`Runtime.txt`: revisión específica de presentación, cuerpos, movimiento y regreso, si ha finalizado con COMPLETE.
`Library/MovementDiagnostics.result`: muestreo de poses y colisiones del islote.

Los scripts de revisión no guardan progresión del usuario. Las copias previas a la migración están en `Library/PresentationBackup/`.
