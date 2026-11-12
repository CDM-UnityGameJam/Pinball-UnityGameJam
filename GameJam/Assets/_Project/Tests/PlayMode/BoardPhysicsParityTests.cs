using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Pinball.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // The new board must move the ball exactly like the old Pinball.unity. The reference paths were
    // recorded in the old scene with the same starts; any change to board geometry, physics materials,
    // the ball or Project Settings physics shows up here.
    public class BoardPhysicsParityTests
    {
        const string GoldenPath = "Assets/_Project/Tests/PlayMode/Golden/LegacyBallPathsV03.txt";
        const float Tolerance = 0.01f;

        static readonly string[] Names = { "straight launch", "8 degree launch", "drop near the top", "roll onto the left guide" };
        static readonly Vector3[] Starts = { new Vector3(0, .455f, -9), new Vector3(0, .455f, -9), new Vector3(3, .7f, 8), new Vector3(-6, .7f, -3) };
        static readonly Vector3[] Velocities = { Vector3.forward * 32, Quaternion.Euler(0, 8, 0) * Vector3.forward * 32, Vector3.zero, Vector3.zero };

        [UnityTest]
        public IEnumerator BallPathsMatchTheOldScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            var context = Object.FindAnyObjectByType<GameContext>();
            float end = Time.realtimeSinceStartup + 6f;
            while (!context.Run.IsPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "timed out waiting for the launch");
                yield return null;
            }

            var golden = LoadGolden();
            // The reference paths were recorded with enemies switched off; compare the board alone.
            foreach (var enemy in context.Enemies.FrontFirst(new System.Collections.Generic.List<Enemy>()))
                enemy.gameObject.SetActive(false);
            // The old scene still had its side return lanes; the side-wall kickers replaced them (they are
            // disabled in Board.prefab). Switch them back on so the comparison covers the same pieces. The
            // extended kicker walls stay in and don't touch these paths.
            foreach (var lane in context.Board.returnLanes)
            {
                lane.gameObject.SetActive(true);
                lane.Initialize(context);
            }
            var board = context.Board.transform;
            var ball = context.Balls.Active[0].Body;
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                for (int s = 0; s < Starts.Length; s++)
                {
                    ball.detectCollisions = true;
                    ball.useGravity = true;
                    ball.isKinematic = false;
                    ball.angularVelocity = Vector3.zero;
                    ball.position = board.TransformPoint(Starts[s]);
                    ball.transform.position = ball.position;
                    ball.linearVelocity = board.TransformDirection(Velocities[s]);
                    Physics.SyncTransforms();
                    for (int i = 0; i < golden[s].Length; i++)
                    {
                        Physics.Simulate(0.01f);
                        var p = board.InverseTransformPoint(ball.position);
                        Assert.That(Vector3.Distance(p, golden[s][i]), Is.LessThan(Tolerance),
                            $"{Names[s]}: step {i + 1} expected {golden[s][i]} got {p}");
                    }
                }
            }
            finally
            {
                ball.isKinematic = true;
                Physics.simulationMode = previousMode;
            }
        }

        static Vector3[][] LoadGolden()
        {
            var lines = File.ReadAllLines(GoldenPath);
            var result = new System.Collections.Generic.List<Vector3[]>();
            foreach (var line in lines)
            {
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                var steps = line.Trim().TrimEnd(';').Split(';');
                var path = new Vector3[steps.Length];
                for (int i = 0; i < steps.Length; i++)
                {
                    var c = steps[i].Split(',');
                    path[i] = new Vector3(Parse(c[0]), Parse(c[1]), Parse(c[2]));
                }
                result.Add(path);
            }
            Assert.That(result.Count, Is.EqualTo(Starts.Length));
            return result.ToArray();
        }

        static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
