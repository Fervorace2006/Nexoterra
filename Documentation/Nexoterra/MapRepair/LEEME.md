# Mapa, regreso y efectos

- 31 superficies de rampas, pasarelas y cimas tienen ahora colisión cerrada con 45 cm de grosor. Son colliders estáticos; no requieren Rigidbody dinámico.
- Navegación de babosas horneada para el mundo. El regreso sigue las esquinas de la ruta por las subidas y puentes y conserva los saltos con comprobación física. Se recalcula cuando el jugador se mueve. La recuperación de emergencia queda para bloqueos prolongados, caídas fuera del mapa o grandes teletransportes.
- Elemental de energía única en Cornisa_0, arriba de la zona inicial. Las especies comunes se sitúan sobre suelo libre fuera de las terrazas sólidas; se conserva su cantidad y especie.
- Techo, paredes y estalactitas cafés. Los suelos mantienen los colores de sus biomas.
- 48 picos destructibles adicionales, 66 en total. Resaltado amarillo suave pulsante. Conservan la reaparición de 300 segundos.
- Cilindros: radio de explosión de 8 m y daño máximo de 110, con caída por distancia. Picos: radio de 3,8 m. Explosiones y polvo más amplios.
- Apuntar conserva la pose y el agarre del arma; la cámara realiza el acercamiento. Ajuste de 1 cm hacia la mano izquierda.
- Agua: charco translúcido y ondas al nivel del suelo; gotas en la estela transformada.
- Elementales: tres anillos orbitales con color propio, tanto salvajes como transformadas, conservando sus efectos originales.

## Comprobaciones

GeometryChecks.txt registra bloqueo desde abajo en las seis subidas, rutas completas desde las entradas a las seis cornisas y cantidad de destructibles. ReturnChecks.txt registra un regreso real desde la zona inicial a la cornisa elevada sin utilizar recuperación de emergencia.

La navegación se hornea al reparar el mapa. Si se cambia la geometría posteriormente, es necesario volver a hornearla; las colisiones locales siguen bloqueando obstáculos durante el salto.
