# Interfaz y guardado manual

Abre Nexoterra_CuatroNodos y pulsa Play. Nueva partida comienza sin criaturas; recoge hongos con E y domestica con F. Las elementales se consiguen en sus biomas, una sola copia por especie.

Esc abre el menú de pausa. Guardar conserva colección, equipo, selección, hongos, posición, salud, personaje y encuentros de jefe completados. Continuar y Cargar usan el último guardado manual. Salir, recoger y equipar no guardan. Nueva partida conserva el archivo anterior hasta que pulses Guardar. El archivo legado se conserva sin cargarlo automáticamente.

El HUD muestra el retrato real del personaje y vida segmentada abajo a la izquierda, seis espacios más pequeños abajo al centro, y Sir Black con vida roja arriba y nombre debajo durante el encuentro. Las barras leen la salud de combate, incluyendo quemadura y explosiones.

El regreso mantiene el ritmo próximo y acelera hasta un 55 % cuando está muy lejos. Una recuperación de seguridad busca un punto libre cercano si la criatura cae bajo el terreno o queda atrapada; termina su regreso saltando y después libera su especie.

Las animaciones son procedurales sobre un rig provisional: caminar, correr, apuntar, retroceso y recarga. Se ajustaron alcance de brazos, muñecas, balanceo y continuidad del ciclo. Los dedos y la voltereta siguen pendientes; los originales no se modifican.

El guardado es un punto de progreso: al cargar se reconstruye el escenario, reaparecen enemigos normales y destructibles y se reinicia el encuentro que estuviera activo. No es una instantánea de cada proyectil ni de todos los temporizadores del mundo.

Validación: ValidacionInterfaz.txt y ValidacionMundoCombate.txt. La captura Interfaz/HUD.png usa una colección temporal para revisar los seis espacios y una instancia de jefe de prueba; no se guarda en la partida del usuario.

La compañera del refugio también tiene un rig derivado y pose de reposo, con brazos relajados y respiración sutil.
