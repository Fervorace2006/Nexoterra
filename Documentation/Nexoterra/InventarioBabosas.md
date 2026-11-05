# Mochila y disparo de babosas

Escena: Assets/00_Scenes/Nexoterra_CuatroNodos.unity.

- E recoge hongos; F alimenta una babosa cercana y la añade a la colección.
- Las primeras seis babosas ocupan automáticamente la barra inferior. Las siguientes van a la mochila.
- 1–6 o la rueda del ratón seleccionan un espacio equipado.
- B o I abren la mochila: selecciona el espacio de destino en la barra y pulsa el retrato que quieres equipar. Si estaba en otro espacio, ambos se intercambian; la desplazada permanece en la colección. Retirar espacio la deja solo en la mochila.
- Clic izquierdo lanza la seleccionada; clic derecho acerca la cámara. El primer clic al recuperar el cursor solo activa la cámara.
- Esc cierra la mochila y libera el cursor. El inventario muestra el total y permite desplazarse cuando hay muchas babosas.

La interfaz usa una paleta de metal oscuro, cian y naranja inspirada en la referencia. Los once retratos transparentes se renderizaron desde los modelos del proyecto; no son recortes de la imagen de referencia.

## Vuelo

Origen en la punta del lanzador, dirección 3D hacia el punto de la mira, incluyendo arriba y abajo, velocidad inicial 24 m/s. A los 8 m se sustituye el modelo por su variante transformada. Al impactar contra un collider del escenario o de un enemigo, la babosa pierde inmediatamente la transformación, cae al suelo y regresa saltando hacia el jugador. Si no impacta, comienza a volver al alcanzar 75 m. La especie permanece bloqueada hasta que llega a menos de 0,85 m del jugador y no hay una pared entre ambos; no se consume la babosa. El bloqueo también afecta a duplicados y se conserva al cambiar el equipamiento, pero permite disparar otras especies. El círculo del retrato es naranja durante el vuelo, celeste durante el regreso y verde cuando está disponible. El progreso del regreso indica proximidad, no un tiempo fijo: puede variar si el jugador se aleja. Hay daño y estados por especie; véase CombateYAnimaciones.md.

La distancia de transformación y velocidad se editan en SlugInventory, en el objeto Jugador. El catálogo está en Assets/NexoterraWorld/Slugs/Catalog.asset. Incluye Araña, Armashelt, Infierno, Gon Doc, Hielo, Tazerling, Carnero y los cuatro elementales, cada uno vinculado a su modelo normal y _T.

## Guardado

El menú inicial ofrece Nueva partida y Continuar. Esc abre pausa con Guardar y Cargar. No hay guardado automático al recoger, equipar o salir. Nueva partida comienza sin babosas y reinicia los encuentros; no borra el archivo guardado. Guardar escribe nexoterra-save-v2.json en Application.persistentDataPath y conserva una copia .bak del guardado anterior. Incluye colección, seis espacios, selección, hongos, posición, salud, personaje y encuentros completados de Sir Black. Cargar reconstruye el mundo: enemigos normales y objetos destructibles se reinician, y un encuentro activo vuelve a empezar. El archivo legado nexoterra-slugs-v1.json se conserva, pero no se carga automáticamente.

Las elementales son únicas y deben domesticarse en su bioma. Cargar una elemental adquirida impide que vuelva a aparecer. Las pruebas usan un perfil independiente.

## Techo y personajes

La bóveda, los picos y las uniones se elevaron 4 m y el perímetro se extendió. La modificación conserva el suelo, los lagos y el volcán. La araña usa el prefab actualizado del usuario.

Los modelos originales se conservan. Las copias generadas tienen un rig provisional de 17 huesos, animación procedural de locomoción, apuntado, disparo y recarga. Los brazos usan IK y las muñecas se orientan con el blaster; todavía no hay dedos articulados ni voltereta.

## Verificación

Comprobado en Play: las once parejas normal/_T, transformación tras 8 m, trayectoria dirigida por la mira, seis espacios automáticos, intercambios sin pérdida de la colección, adopción mediante comida, origen del disparo, recarga y colisiones. Se revisaron visualmente la barra y la mochila en pantalla ancha. Capturas en Inventario/01_barra.png e Inventario/02_mochila.png; contienen una colección temporal usada para las pruebas.

Las babosas salvajes avanzan con saltos cortos y pausas. Las que regresan dan saltos más rápidos, buscan superficies de apoyo y prueban direcciones alternativas ante obstáculos. El movimiento incluye una ligera compresión del modelo al saltar y aterrizar; no necesita un esqueleto.

Validación del regreso: se comprobó bloqueo por especie con duplicados y cambios de equipamiento, disparo simultáneo de otra especie, pérdida de transformación al impactar, saltos de regreso hacia un jugador en movimiento y posibilidad de volver a disparar al llegar. También se verificó el regreso al agotar el alcance y al impactar antes de transformarse. Informe: ValidacionRegreso.txt.

## Actualización de combate y rig

Se añadieron enemigos, destructibles, daño y animación mediante rigs provisionales. R recarga y Home vuelve al inicio. Los detalles y límites actuales están en CombateYAnimaciones.md; esta actualización sustituye las notas anteriores sobre modelos sin rig o ausencia de daño.

Los regresos a más de 18 m aceleran gradualmente hasta 1,55 veces a 65 m. Si una ruta se atasca cinco segundos, una caída queda muy por debajo del jugador o pasan veinte segundos sin llegar, la babosa se recoloca en un apoyo libre próximo y termina saltando. La especie no se libera antes de llegar.
