# Animaciones y jugabilidad

Ctrl+A / Ctrl+D: volteretas izquierda/derecha. El controlador conserva el desplazamiento y respeta obstáculos; el clip ya no duplica la traslación.

Ambos jugadores tienen reposo con piernas neutrales, marcha adaptada de Pistol_Run, carrera, volteretas y muerte. La cadencia acompaña al desplazamiento. Se añadió IK de pies en apoyo y mano secundaria; el arma sigue la mano derecha y la inclinación al apuntar.

Enemy1 y Sir Black reutilizan el controlador humano. Enemy1 no rueda; Sir Black puede hacerlo cada nueve segundos cuando está cerca y puede moverse. Sir_Black_Final sigue estático porque no tiene avatar humano válido.

No se recibieron clips específicos de caminar, recargar o disparar. La marcha es una adaptación de la carrera; para pulir más los apoyos y desplazamientos laterales conviene un clip dedicado.

## Efectos transformados

| Babosa | Efecto |
| --- | --- |
| Araña | Telaraña del ZIP en los pies; inmoviliza 5,5 s y permite disparar. |
| Hielo | Cubo transparente; bloquea movimiento y disparo 4 s. |
| Infierno / elemental de fuego | Daño y quemadura; derrite el hielo. |
| Elemental de agua | Círculo de agua; inmoviliza 4 s y permite disparar. |
| Tornado / elemental de aire | Espirales y arrastre durante 3 s, limitado por colisiones. |
| Electroshock | Inmoviliza e impide disparar 4,5 s, con los rayos y chispas de la babosa alrededor del cuerpo. |

El barrido continuo del proyectil usa radios de 0,16 m pequeño y 0,42 m transformado. Incluye solapamientos iniciales, descarta al tirador y respeta el primer obstáculo.

## Escenas y validación

Nexoterra_Menu es la primera escena de compilación y la entrada al pulsar Play. Solo contiene cámara y menú. Historia/continuar cargan el mundo asincrónicamente mostrando Resources/Nexoterra/Loading.jpg con progreso y puntos animados. Esc abre la pausa para guardar manualmente y volver al menú. Las pruebas no modificaron partidas guardadas.

Checks.txt registra validación aislada de efectos, expiración, descongelación, arrastre, roces, muros, deformación de cuatro esqueletos, desplazamiento persistente de voltereta y separación del menú. Se revisaron capturas de poses y se abrió menú e historia en el editor principal.

Referencias: [Root motion de Unity](https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/avatar-creationand-setup/root-motion), [LoadSceneAsync](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/scenemanagement/scenemanager/loadsceneasync).
