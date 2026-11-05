# Hábitats y especies

Escena: `Assets/00_Scenes/Nexoterra_CuatroNodos.unity`.

Se mantienen los cuatro nodos, los seis biomas y el relieve original. Se añadieron terrazas de roca estratificada, subidas, puentes con barandas, helechos y hongos de tallos curvos. El material incorpora variación orgánica y puntos luminosos en los sombreros.

## Distribución

| Zona | Babosas disponibles | Santuario |
| --- | --- | --- |
| Jardines del Nexo | Tazerling, Gon Doc, Armashelt, Carnero, Araña | Se conserva la especie de energía al inicio |
| Cavernas de hielo | Hielo, Armashelt, Carnero, Gon Doc, Elemental Aire | Cornisa elevada de hielo |
| Tierras antiguas | Carnero, Araña, Armashelt, Gon Doc, Elemental Tierra | Cornisa de roca |
| Caldera volcánica | Infierno, Tazerling, Armashelt, Carnero, Elemental Fuego | Borde de la cima del volcán |
| Galería acuática | Gon Doc, Hielo, Araña, Armashelt, Elemental Agua | Islote con acceso y lago a nivel del terreno |
| Ciénaga carmesí | Infierno, Araña, Tazerling, Carnero | Zona final |

Son 29 puntos, una babosa por punto y especies distintas dentro de cada zona. Los spawns se activan a 110 metros. Hay cuatro elementales en los recursos actuales; cada una tiene un único punto y no vuelve a aparecer una vez domesticada, incluso después de guardar y cargar.

## Modelos reparados

Infierno usa `Assets/02_Prefabs/Infierno.fbx`; su transformación usa `Assets/02_Prefabs/Infierno_T.prefab`. La transformación de agua usa el nuevo `Assets/02_Prefabs/ELEMENTAL_AGUA_T.fbx`, orientado hacia adelante. Los prefabs visuales generados se desvinculan de prefabs anidados para evitar las referencias eliminadas. Las transformaciones se ajustan por su dimensión máxima para no sobredimensionar modelos anchos.

El inventario migra automáticamente Burpy a Infierno y unifica elementales duplicadas en guardados anteriores. No se borra la colección del jugador.

## Edición

Los añadidos están bajo `Senderos y santuarios`; las apariciones bajo `Spawns de babosas por bioma`. Recursos en `Assets/NexoterraWorld/Habitats`.

El menú **Nexoterra → Mundo → Mejorar habitats y reparar especies** reconstruye estos grupos generados y repara el catálogo. Antes de hacerlo guarda una copia de la escena en `Library/NexoterraAntesHabitats_*.unity`. Los cambios manuales dentro de esos grupos se reemplazan al reconstruir.

El lago toma su altura del terreno, no de la cima del islote. Un borde con colisión une el agua con el suelo circundante.

## Verificación

Informes en `ValidacionHabitats.txt` y `ValidacionHabitatsPlay.txt`. Las pruebas de persistencia usan una copia del proyecto con un perfil de guardado separado. Capturas en `Habitats/`.
