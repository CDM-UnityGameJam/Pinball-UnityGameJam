using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BallContactReport
{
    [MenuItem("LuckyPinball/Report Live Ball Contacts")]
    public static void Run()
    {
        var gm = GameManager.Instance;
        var report = new StringBuilder();
        report.AppendLine("Playing=" + Application.isPlaying + " timeScale=" + Time.timeScale);
        if (gm)
        {
            report.AppendLine("GamePlaying=" + gm.IsPlaying + " GameOver=" + gm.IsGameOver);
            foreach (var ball in gm.Balls)
            {
                if (!ball) continue;
                report.AppendLine(ball.name + " local=" + gm.board.InverseTransformPoint(ball.position) +
                    " velocity=" + ball.linearVelocity + " kinematic=" + ball.isKinematic + " sleeping=" + ball.IsSleeping() +
                    " detect=" + ball.detectCollisions + " constraints=" + ball.constraints);
                if (ball.TryGetComponent<SideReturnBall>(out var state)) report.AppendLine("InTransit=" + state.InTransit);
                var shape = ball.GetComponent<Collider>();
                foreach (var other in Physics.OverlapSphere(ball.position, .6f, ~0, QueryTriggerInteraction.Collide))
                {
                    if (other.attachedRigidbody == ball) continue;
                    bool penetrating = Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out var direction, out var distance);
                    if (!penetrating) { direction = Vector3.zero; distance = 0f; }
                    report.AppendLine("NEAR " + other.name + " trigger=" + other.isTrigger + " penetration=" + penetrating + " depth=" + distance + " dir=" + direction);
                }
            }
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/live-ball-contacts.txt"), report.ToString());
        Debug.Log("Live ball contact report saved.");
    }
}
