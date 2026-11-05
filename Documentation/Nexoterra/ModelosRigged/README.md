# Modelos con esqueleto importado

Sustituidos Player_Male, Player_Female, Enemy1 y Sir_Black con los FBX y texturas de los cuatro ZIP entregados por el usuario. Los fuentes están en Assets/Characters/Rigged; los prefabs normalizados y materiales URP están en Assets/NexoterraWorld/Characters.

Se conservan los esqueletos y pesos originales. Importación Generic, sin clips ni controlador de animación; Animator desactivado para mantener la pose base. No se generaron huesos ni se reutilizó el rig provisional. La configuración Humanoid y sus animaciones se prepararán en un paso posterior.

Alturas: jugador 1,90 m, chica 1,85 m, enemigo 1,90 m y Sir Black 2,20 m. La compañera del refugio también usa Player_Female. Los dos retratos del HUD se generaron nuevamente.

La escena conserva los controles, inventario, vida, spawns y encuentros. Se retiró PlayerAnimationDriver del jugador, las referencias HumanoidPose y el balanceo procedural del enemigo. Los personajes permanecen en pose base mientras sus controladores de juego los desplazan; todavía no hay caminar, correr, apuntar ni recargar animados.

Se reemplazaron los prefabs de combate conservando sus GUID, así como Assets/02_Prefabs/Enemy1.prefab y Boss_Sir_Black.prefab. Los modelos anteriores y las mallas del rig provisional se retiraron de Assets después de comprobar que ninguna escena o prefab los referenciaba. Respaldo fuera del proyecto: D:/Realidad Virtual/Nexoterra_Backups/FuentesAnteriores_20261101_180352. Copia adicional previa: ModelosAnteriores_20261101_175516.zip en la misma carpeta de respaldos.

Validacion.txt incluye los huesos asignados y las verificaciones de importación. Las cuatro imágenes muestran los modelos nuevos con sus texturas y pose original.
