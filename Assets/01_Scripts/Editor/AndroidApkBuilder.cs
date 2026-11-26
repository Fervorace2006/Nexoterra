using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;

namespace Nexoterra.EditorTools
{
    [InitializeOnLoad]
    public static class AndroidApkBuilder
    {
        const string RequestPath = "Library/NexoterraAndroidBuild.request";
        const string ResultPath = "Library/NexoterraAndroidBuild.result";
        const string OutputPath = "Build/Android/Nexoterra.apk";

        static AndroidApkBuilder() => EditorApplication.update += PollRequest;

        [MenuItem("Nexoterra/Build Android APK")]
        public static void BuildFromMenu() => Build();

        [MenuItem("Nexoterra/Polish Mobile Scenes and Build APK")]
        public static void PolishScenesAndBuild()
        {
            int houses = EmbedArenaHouses();
            int enemies = DisperseFinalEnemies();
            var report = Build();
            Debug.Log("Arena: casas ajustadas " + houses + ". Nexoterra Final: enemigos dispersados " + enemies + ". APK: " + OutputPath + " (" + report.summary.totalSize + " bytes).");
        }

        static void PollRequest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            try
            {
                int houses = EmbedArenaHouses();
                int enemies = DisperseFinalEnemies();
                var report = Build();
                File.WriteAllText(ResultPath, "PASS " + OutputPath + " · " + report.summary.totalSize + " bytes · casas " + houses + " · enemigos " + enemies);
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, "FAIL " + exception);
                Debug.LogException(exception);
            }
        }

        static BuildReport Build()
        {
            PlayerSettings.companyName = "Nexoterra";
            PlayerSettings.productName = "Nexoterra";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nexoterra.game");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No hay escenas habilitadas en EditorBuildSettings.");
            var fullOutput = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullOutput,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("El build Android terminó con estado " + report.summary.result + ". Revisa Editor.log.");
            return report;
        }

        static int EmbedArenaHouses()
        {
            var scene = OpenSceneSafely("Assets/00_Scenes/Campaign/Nexoterra_Arena.unity", out var opened);
            var active = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var roots = scene.GetRootGameObjects();
                var ground = roots.SelectMany(root => root.GetComponentsInChildren<MeshCollider>(true)).FirstOrDefault(collider => collider && collider.name == "Suelo del bioma" && collider.sharedMesh);
                var village = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(transform => transform && transform.name == "[Aldea multiplayer]");
                if (!ground || !village) throw new InvalidOperationException("Falta el suelo o la aldea guardada en Nexoterra_Arena.");

                int adjusted = 0;
                var houses = village.GetComponentsInChildren<Transform>(true).Where(transform => transform && transform.name == "Casa cúpula").ToArray();
                foreach (var house in houses)
                {
                    var renderers = house.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer && renderer.enabled).ToArray();
                    if (renderers.Length == 0) continue;
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    var ray = new Ray(new Vector3(house.position.x, ground.bounds.max.y + 80, house.position.z), Vector3.down);
                    if (!ground.Raycast(ray, out var hit, 240)) continue;
                    float correction = hit.point.y - bounds.min.y - .12f;
                    if (Mathf.Abs(correction) < .005f) continue;
                    house.position += Vector3.up * correction;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(house);
                    adjusted++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return adjusted;
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        static int DisperseFinalEnemies()
        {
            var scene = OpenSceneSafely("Assets/00_Scenes/Campaign/Nexoterra_Final.unity", out var opened);
            var active = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var level = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignLevel>(true)).FirstOrDefault(candidate => candidate && candidate.index == 6);
                if (!level) throw new InvalidOperationException("No se encontró el nivel 7 en Nexoterra_Final.");
                var enemies = level.enemies.Where(enemy => enemy).Select(enemy => enemy.transform).Distinct().ToArray();
                if (enemies.Length < 2) throw new InvalidOperationException("No hay suficientes enemigos para dispersar en Nexoterra_Final.");

                Vector3 center = Vector3.zero;
                foreach (var enemy in enemies) center += enemy.position;
                center /= enemies.Length;
                float nearest = float.MaxValue;
                for (int i = 0; i < enemies.Length; i++) for (int j = i + 1; j < enemies.Length; j++)
                    nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(enemies[i].position.x, enemies[i].position.z), new Vector2(enemies[j].position.x, enemies[j].position.z)));
                if (nearest >= 20f) return 0;
                const float spread = 1.35f;
                foreach (var enemy in enemies)
                {
                    var position = enemy.position;
                    position.x = center.x + (position.x - center.x) * spread;
                    position.z = center.z + (position.z - center.z) * spread;
                    enemy.position = position;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return enemies.Length;
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        static Scene OpenSceneSafely(string path, out bool opened)
        {
            var scene = SceneManager.GetSceneByPath(path);
            opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("No se pudo abrir " + path);
            return scene;
        }
    }
}
