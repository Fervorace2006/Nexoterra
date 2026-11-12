# Historia y multijugador

El botón Play de Unity está configurado para abrir el menú. Esta preferencia también se puede activar desde `Nexoterra → Historia → Iniciar Play desde el menú`.

La historia comienza desde `Nexoterra_Menu` → **Iniciar historia**. Las partidas antiguas sin escena siguen cargando el mapa combinado original.

## Escenas

| Nivel | Bioma / escena | Guardianes y elemental |
|---|---|---|
| 1 | Jardines / Nexoterra_Jardines | Un secuaz con Energía malvada |
| 2 | Hielo y aire / Nexoterra_Hielo | Un secuaz con Aire malvada |
| 3 | Agua / Nexoterra_Agua | Un secuaz con Agua malvada; tres lagunas |
| 4 | Tierra / Nexoterra_Tierra | Un secuaz con Tierra malvada |
| 5 | Fuego / Nexoterra_Volcan | Un secuaz como trampa; al vencerlo aparece Sir Black 1 con Fuego malvada |
| 6 | Agua oscura / Nexoterra_Carmesi | Siete Enemy2, un secuaz y Sir Black 2; final de la historia |

Los primeros cinco niveles contienen siete Enemy1 cada uno. Solo hay un secuaz por nivel. Sir Black 1 aparece únicamente en el quinto nivel; Sir Black 2, únicamente en el sexto. Ya no hay un séptimo nivel en la progresión ni en Build Settings; el archivo antiguo Nexoterra_Final se conserva, y las partidas guardadas allí se redirigen a Ciénaga carmesí.

Cada bioma mantiene sus 250 × 250 metros y las especies silvestres que ya estaban configuradas. Se reutilizan los controladores y clips de los enemigos anteriores: reposo, caminar, correr, apuntar, recargar, caídas, levantarse, muerte y las evasiones de los jefes. Los nuevos FBX humanoides están en Assets/Characters/Rigged; sus prefabs de combate están en Assets/NexoterraWorld/Combat.

Vida: Enemy1 160, Enemy2 180, secuaz 220, Sir Black 1 500 y Sir Black 2 750. Los tres modelos nuevos usan una copia del arma del jugador, calibrada sobre la mano derecha de cada esqueleto.

Cada enemigo tiene tres espacios de babosas malvadas del bioma. La elemental ocupa uno de los espacios del guardián correspondiente. Un espacio permanece ocupado mientras la babosa vuela o regresa saltando; no se permite un cuarto disparo con los tres espacios ocupados. Al morir, el enemigo deja sus babosas en el suelo y cancela sus proyectiles activos para evitar duplicados. Los enemigos no pagan vida por usar malvadas; el jugador conserva el coste del 1% y el aumento del 5% de daño.

El túnel está al extremo opuesto de la entrada y aparece con una flecha amarilla en el mapa (`M`). Exige derrotar a todos los enemigos y, cuando corresponde, curar y domesticar la elemental. El primer bioma incluye Doc cerca de la entrada. Se recoge comida con `E`, se cura disparando Doc/Energía y se domestica con `F`. La transición conserva inventario, equipamiento y comida, y restaura la vida al entrar al siguiente bioma.

El guardado de historia sigue siendo manual desde pausa. Guarda también la escena, los enemigos derrotados y qué botín se recogió. Al cargar una partida de la versión anterior de los encuentros, se conservan colección y posición, pero se reinician los enemigos para no interpretar los índices antiguos como los guardianes nuevos. La colección para multijugador se conserva automáticamente al domesticar una babosa, en un archivo separado: `nexoterra-collection-v1.json` dentro de `Application.persistentDataPath`. Cada equipo conserva su propio perfil; no hay sincronización de partidas en la nube.

## Salas por Internet

En el menú: **Multijugador → Crear sala**. Comparte el código con los otros jugadores; ellos usan **Entrar con código**. Capacidad: cuatro jugadores contando al anfitrión. El anfitrión debe mantener el juego abierto. Se usa Unity Relay con DTLS, autenticación anónima y Netcode for GameObjects. La arena es PvP; la campaña permanece individual.

La arena carga las babosas adquiridas en historia sin sobrescribir esa partida. Sin colección previa se entrega Infierno. Movimiento, orientación, estados de animación, disparos y daño viajan por la sala; el anfitrión calcula vida y reaparición. Salir de la sala regresa al menú.

Paquetes fijados: `com.unity.netcode.gameobjects` 3.2.1 y `com.unity.services.multiplayer` 2.4.1. El proyecto utiliza su vínculo existente con Unity Gaming Services.

## Babosas malvadas

Incremento del 5% sobre su daño anterior, incluido el daño de quemadura. Cada lanzamiento del jugador consume el 1% de la vida máxima y conserva al menos 1 punto de vida. El coste se muestra en la descripción de la mochila.

## Comprobaciones

`CampaignValidation` se ejecuta bajo petición mediante `Library/CampaignValidation.request`, sin guardar la partida del usuario. Comprueba los seis niveles, dimensiones, enemigos, recompensas, purificación, bloqueo/desbloqueo y captura del estado. También crea una sala real de Relay, conecta otro cliente de prueba y verifica el envío de disparos y los impactos reales contra el anfitrión y el cliente. Esta comprobación usa dos conexiones en la misma máquina; no sustituye una prueba entre dos equipos físicos.

El resultado se escribe en `Library/CampaignValidation.result`. Las capturas nuevas de los modelos están en NewEnemyPreviews; las capturas Level*.png son de la versión anterior de la campaña. Los cambios compilan en Unity 6000.5.6f1; las advertencias de APIs obsoletas existentes no se han eliminado como parte de esta tarea.

`CampaignBuilder` regenera los niveles desde el mapa original únicamente con `Library/CampaignBuild.request`. Regenerar reemplaza las escenas de campaña generadas; no se ejecuta automáticamente al abrir el proyecto.


La prueba específica `CampaignEnemyValidation` se activa con `Library/CampaignEnemyValidation.request`. Comprueba límites de slots, retorno real, botín, especies por bioma, avatares, coste cero de los enemigos, emboscada y progresión. Los resultados están en ValidationEnemies.txt y las capturas de animación en NewEnemyPreviews. La petición con contenido `water` comprueba solo el tercer nivel; `models` genera capturas de los tres modelos nuevos.

`CampaignEnemyInstaller` actualiza modelos y encuentros solo bajo petición `Library/CampaignEnemies.request`, conservando una copia de las escenas previas en Library/CampaignEnemiesBackup. `CampaignWaterZones` actualiza las lagunas y su navegación con `Library/CampaignWater.request`.

