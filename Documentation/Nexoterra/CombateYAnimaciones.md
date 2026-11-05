# Combate, destrucción y animaciones provisionales

Escena: `Assets/00_Scenes/Nexoterra_CuatroNodos.unity`.

## Controles

WASD mueve, Mayús corre, clic derecho apunta, clic izquierdo dispara, R reproduce la recarga de 1,1 segundos. Durante esa recarga no se dispara; no recupera antes de tiempo las babosas que todavía regresan. Home vuelve al inicio. V alterna los personajes. La voltereta lateral queda pendiente.

Ambos jugadores y los enemigos tienen un rig provisional de 17 huesos con mallas derivadas. La pose se calcula por código: piernas al caminar/correr, brazos sujetos al bláster mediante cinemática inversa, orientación al apuntar, retroceso al disparar y brazo de apoyo al recargar. No se modificaron los FBX originales. Los pesos automáticos son una base de prototipo: todavía necesitan ajuste artístico en hombros, codos y ropa para igualar las animaciones de referencia; no incluyen dedos ni animación facial.

## Efectos de babosas

El destello aparece al transformarse. La estela y las partículas se concentran detrás de la cola para mantener visible la cara. Se apagan al impactar o terminar el vuelo, y sus restos desaparecen solos.

| Especie | Daño de impacto | Estado |
| --- | --- | --- |
| Infierno | 22 | Quemadura: 5 por segundo, 4 segundos |
| Elemental fuego | 32 | Quemadura: 7 por segundo, 5 segundos |
| Elemental agua | 20 | Velocidad al 55%, 4 segundos |
| Hielo | 18 | Velocidad al 45%, 4,5 segundos |
| Carnero | 28 | Derribo 2,2 segundos y empuje |
| Armashelt | 25 | Derribo 1,8 segundos y empuje |
| Elemental tierra | 30 | Derribo 1,5 segundos y empuje |
| Elemental aire | 20 | Empuje mayor |
| Tazerling | 18 | Aturdimiento 1,2 segundos |
| Araña | 12 | Velocidad al 25%, 3 segundos |
| Gon Doc | 12 | Impacto básico |

Los valores están en `SlugCatalog`, dentro del perfil `combat` de cada especie. Las ralentizaciones no se multiplican y las quemaduras repetidas refrescan duración sin sumar daño por segundo. Un impacto antes de transformarse hace 3 de daño y no aplica la habilidad.

## Objetos del mapa

Hay 42 objetos separados por los seis biomas: 18 picos de techo, 12 barreras de roca y 12 cilindros explosivos provisionales. Algunos picos miden hasta 4,3 metros; el resto del techo se elevó 1,5 metros adicionales una sola vez.

Carnero, Armashelt y Elemental Tierra transformadas rompen las rocas. Los picos del techo caen antes de producir una explosión de tierra al aterrizar. Picos y barreras hacen hasta 35 de daño en un radio de 2,8 metros. Los cilindros reaccionan a cualquier babosa transformada: hasta 85 de daño en 6 metros, fuego, humo y explosiones en cadena.

El daño disminuye hacia el borde y se aplica una vez por actor aunque tenga varios colliders. Afecta al jugador y a los enemigos. Cada objeto reaparece a los 300 segundos de juego en su posición original; si un actor ocupa el lugar, se retrasa la reaparición hasta despejarlo. Los temporizadores de objetos son de la sesión actual.

## Enemigos

Once Enemy 1 están distribuidos por el mapa. Patrullan, se acercan, comprueban línea de visión y disparan. Su movimiento respeta ralentización y derribo. Reaparecen cinco minutos después de morir. Es una IA básica con CharacterController, todavía sin navegación estratégica ni búsqueda de rutas complejas.

Sir Black tiene tres encuentros secuenciales en la Ciénaga carmesí, con una sola instancia activa. Se retira al bajar del 40% de vida en los dos primeros; el tercero permite derrotarlo. Alterna versiones malvadas de Infierno, Hielo y Tazerling, con daño de impacto un 35% mayor y estelas violetas. Son variantes de los modelos existentes, no nuevas especies coleccionables. La progresión de encuentros completados se incluye al pulsar Guardar en el menú; no hay autoguardado durante la partida.

El jugador tiene 100 de vida; tras morir vuelve al inicio después de dos segundos y conserva su colección. Los tres objetivos de práctica junto al inicio permiten comprobar daño y estados sin empezar un encuentro.

## Organización y edición

- `SlugProjectile`: vuelo, transformación, impacto y regreso.
- `SlugCombatProfile`: valores editables de habilidades.
- `EnemyCombatant` y `PlayerHealth`: vida y estados.
- `AreaDamage` / `IDamageReceiver`: daño de área sin duplicar actores.
- `WorldDestructible`: caída, explosión y reaparición.
- `EnemyShooter` y `BossEncounters`: IA y secuencia del jefe.
- `HumanoidPose` y `PlayerAnimationDriver`: poses y controles de animación.
- `SlugFlightEffects`: partículas, estelas y humo.

Recursos generados en `Assets/NexoterraWorld/Combat`. El menú Nexoterra → Combate → Instalar enemigos destructibles y animación guarda una copia previa de la escena en Library. Conserva grupos ya instalados y actualiza los prefabs generados de enemigos; no modifica los modelos fuente.

Informes de validación en `ValidacionCombate.txt` y `ValidacionMundoCombate.txt`. Las pruebas usan un proyecto y perfil de guardado separados.

