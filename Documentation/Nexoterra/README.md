# Nexoterra · Cuenca del Nexo

La versión ampliada actual está en `Assets/00_Scenes/Nexoterra_CuatroNodos.unity`. Véase [CuatroNodos.md](CuatroNodos.md). Esta página describe la cuenca original conservada.

Mapa 3D original de fantasía subterránea, inspirado en la temática de Bajoterra. Creado dentro del proyecto existente con geometría y materiales propios, sin paquetes de pago ni modelos descargados.

## Abrir y recorrer

1. Abrir `Assets/00_Scenes/Nexoterra_CuencaDelNexo.unity`.
2. Pulsar **Play** y hacer clic dentro de **Game**.
3. **WASD** mueve, **ratón** mira, **Mayús** corre, **Espacio** salta.
4. **Esc** libera el cursor, **R** vuelve al inicio, **H** oculta la ayuda.

El explorador mide 2 m. Es una cámara de prueba en primera persona para revisar el mapa; no sustituye al personaje final del GDD. No hay todavía combate, natación ni multijugador.

## Distribución

Unidades: metros, eje Y vertical. La cuenca original guardada mide **320 × 320 m**, desde −160 hasta 160 en X y Z. La nueva escena CuatroNodos reúne cuatro copias de esta base y mide 640 × 640 m.

| Lugar | Centro X Z | Características |
| --- | --- | --- |
| Entrada | −9, −52 | Explanada y vista hacia el acuífero |
| Acuífero principal | −55, −22 | Agua celeste, orilla mineral y cristales |
| Acuífero norte | −95, 64 | Segunda laguna celeste |
| Cuenca carmesí | 83, 34 | Agua roja oscura, roca y cristales cálidos |
| Laguna carmesí norte | 105, 94 | Cuenca separada al noreste |
| Meseta del Nexo | 22, 2 | Cima plana a 5,6 m y arco de piedra |
| Otras mesetas | Ver objetos Puntos de interes | Cinco elevaciones con cimas verdes y acceso por ladera |

La bóveda se encuentra aproximadamente a 12–14 m sobre la explanada y se eleva donde lo necesitan las mesetas. Sus estalactitas miden 1,3–2,8 m: mantienen una altura libre superior a la del personaje incluso sobre las montañas. Las montañas utilizan transiciones suaves y una cima matemáticamente plana. Los bordes exteriores tienen paredes con colisión.

## Recursos editables

- `Assets/NexoterraWorld/Meshes`: suelo en 64 sectores, techo en 64 sectores y grupos de decoración. Son assets guardados, no generación en cada partida.
- `Assets/NexoterraWorld/Materials`: paletas del suelo y roca, vegetación con movimiento, cristales emisivos y las dos aguas.
- `Assets/NexoterraWorld/Shaders/CavernSurface.shader`: sombreado URP con color por vértice, estratos, grano, emisión y brisa.
- `Assets/NexoterraWorld/Shaders/CavernWater.shader`: ondas suaves, patrones animados de luz y espuma perimetral; no requiere texturas externas.
- `Assets/NexoterraWorld/CavernAtmosphere.asset`: bloom, contraste y tonemapping.
- `Assets/01_Scripts/World/CavernExplorer.cs`: movimiento con el Input System ya instalado.
- `Assets/01_Scripts/Editor/NexoterraCavernBuilder.cs`: construcción reproducible con semilla fija y validación de pendientes y materiales.

Las superficies acuáticas son estilizadas y opacas, con colores de profundidad; no simulan refracción o fluidos. Los volúmenes de agua son referencias para integrar posteriormente natación, daño o reglas de combate. Los cristales usan emisión y bloom; no colocan cientos de luces dinámicas. La vegetación se agrupa en mallas por sector para reducir objetos y llamadas de dibujo.

## Regeneración

El menú **Nexoterra → Mapa** permite generar la cuenca, validarla y capturar vistas. **Guardar primero la escena activa**. Regenerar reconstruye los recursos generados y la escena; no usar esta opción después de modificar manualmente esos recursos sin una copia o commit previo. El generador conserva una copia del archivo de escena anterior en `Library`, pero esa copia no protege versiones anteriores de las mallas.

Se conserva `SampleScene`. Los ajustes de renderizado y los cambios que ya tenía el proyecto no se sustituyen.

## Guardar los cambios con tu identidad Git

No se ha hecho commit ni push. El repositorio ya tenía identidad de autor configurada. Verificarla antes de confirmar:

```powershell
Set-Location 'D:\Realidad Virtual\Nexoterra'
git config user.name
git config user.email
git status --short
```

Para seleccionar solamente los archivos nuevos del mapa:

```powershell
git add -- Assets/00_Scenes/Nexoterra_CuencaDelNexo.unity Assets/00_Scenes/Nexoterra_CuencaDelNexo.unity.meta Assets/NexoterraWorld Assets/NexoterraWorld.meta Assets/01_Scripts/World Assets/01_Scripts/World.meta Assets/01_Scripts/Editor Assets/01_Scripts/Editor.meta Documentation/Nexoterra
git diff --cached --stat
git commit -m "Agrega mapa cavernoso de Nexoterra con mesetas y aguas estilizadas"
```

La identidad del commit es distinta de la cuenta utilizada para autenticar el push. Ejecutar `git push` desde tu sesión habitual, una vez revisado el commit y el remoto configurado. No se modificaron credenciales ni configuración global de Git.
