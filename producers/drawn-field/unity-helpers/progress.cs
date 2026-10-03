var captures = UnityEngine.Resources.FindObjectsOfTypeAll<Capture.GameView.GameViewEpisodeCapture>();
var ship = UnityEngine.Object.FindFirstObjectByType<Ships.Ship>();
return new { time=UnityEngine.Time.time, bank=ship ? ship.Kinematics.bank : 0, captureDirs=System.Array.ConvertAll(captures, c => c.FrameDir) };
