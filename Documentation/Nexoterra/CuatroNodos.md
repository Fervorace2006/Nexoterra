# Nexoterra: cuatro nodos y seis biomas

Escena actual: `Assets/00_Scenes/Nexoterra_CuatroNodos.unity`.

Cuatro nodos de 320 × 320 metros en una cuadrícula 2 × 2: extensión total de 640 × 640 metros y cuatro veces la superficie de la cuenca original. Se conservaron sus relieves, lagos y decoración como base, retirando paredes interiores para comunicar los nodos. La cuenca original sigue guardada por separado.

Vista desde arriba, con +Z hacia el norte:

| Oeste | Este |
| --- | --- |
| Tierra: montañas y techo ocre | Volcán: cráter, lava, suelo y techo rojizos |
| Hielo: techo helado y agujas inclinadas | Acuática: suelo mineral turquesa y lagos azules |
| Final carmesí: suelo oscuro y agua roja | Inicio verde: mesetas y lagunas celestes |

El personaje aparece al sureste, cerca de (275, −270). La bóveda se elevó 4 metros adicionales en todos los biomas, junto con las estalactitas y uniones; se mantiene el espacio extra sobre el volcán. Hay 29 puntos de aparición: cinco especies distintas por bioma, excepto Ciénaga carmesí, que tiene cuatro. Cada punto genera una sola babosa. Se activan al acercarse y se descargan al alejarse.

## Jugar

Abrir la escena, pulsar Play y hacer clic en Game.

| Control | Acción |
| --- | --- |
| WASD / Mayús / Espacio | Mover / correr / saltar |
| Ratón | Cámara en tercera persona con colisión |
| Clic derecho / izquierdo | Apuntar / lanzar la babosa seleccionada |
| V | Alternar militar y comando femenina |
| E | Recoger un hongo pequeño cercano |
| F | Alimentar y adoptar una babosa cercana usando un hongo |
| 1–6 / rueda | Seleccionar babosa equipada |
| B o I | Abrir mochila e intercambiar babosas |
| M | Mapa y ubicación del jugador |
| T | Visitar el siguiente bioma para revisar el escenario |
| Home / Esc / H | Regresar al inicio / liberar ratón / ocultar ayuda |

Hay hongos comestibles y hongos arbóreos luminosos junto a terrazas, senderos y puentes. La comida reaparece tras 90 segundos; las especies comunes se reponen cuando el jugador está cerca. Las cuatro elementales son únicas y dejan de aparecer cuando están domesticadas, también al cargar la partida. Véase Habitats.md.

## Alcance del prototipo

Se reutilizaron los modelos militares, comando femenina, Blast y babosas importados por el usuario. Los originales no traen clips de animación. Las copias del jugador y los enemigos usan rigs provisionales y poses procedurales; las babosas saltan por código. Véase CombateYAnimaciones.md.

La adopción añade cada babosa a una mochila persistente con seis espacios equipados y retratos del modelo. La babosa seleccionada sale del bláster hacia el punto de la mira y cambia a su modelo `_T` al recorrer 8 metros. El disparo tiene colisión con el escenario; aplica daño y estados por especie (véase CombateYAnimaciones.md). El agua no tiene sistema de natación. Los biomas están conectados y abiertos; el orden de historia y la progresión aún no bloquean rutas.

## Editar

Los objetos están organizados bajo `NEXOTERRA · Cuatro nodos · 640 x 640 m`. Recursos nuevos en `Assets/NexoterraWorld/Biomes`; scripts en `Assets/01_Scripts/World`. Las capturas están en `Documentation/Nexoterra/Biomes`.

El menú Nexoterra → Mundo permite validar y capturar. Construir cuatro nodos reconstruye la escena y sus recursos generados: conservar una copia antes de hacer cambios manuales. No se hizo commit ni push.

## Validación realizada

La validación de estructura y materiales pasó. En una copia del proyecto se comprobó en Play el contacto con el suelo en los seis puntos de visita, aparición de babosas, recogida de hongos, adopción consumiendo comida, trazo del arma y presencia de ambos personajes. Los informes están en ValidacionBiomas.txt y ValidacionPlayMode.txt. Las seis capturas se revisaron visualmente.

## Actualización de combate y rig

Se añadieron enemigos, destructibles, daño y animación mediante rigs provisionales. R recarga y Home vuelve al inicio. Los detalles y límites actuales están en CombateYAnimaciones.md; esta actualización sustituye las notas anteriores sobre modelos sin rig o ausencia de daño.
