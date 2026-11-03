#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > Ludi > Probar torneo (guardado y carga)
//
// Juega 25 torneos completos sin abrir ninguna escena: el "jugador" gana o pierde al azar y elige
// mejoras al azar. Después de CADA paso olvida el torneo de la memoria (como si se cerrara el juego),
// lo recupera con TryResume y comprueba que todo sigue igual. Además verifica las reglas:
//  - el cuadro siempre termina con un campeón; los demás equipos acaban con 2 derrotas, nadie con más;
//  - antes de cada partido el jugador ha hecho exactamente las elecciones de mejora que le tocan;
//  - el estado del guardado es "en curso" mientras se juega y "terminado" al final.
// Tu partida guardada real se copia antes y se deja como estaba al acabar.
public static class TournamentSelfTest
{
    const int Runs = 25;

    [MenuItem("Tools/Ludi/Probar torneo (guardado y carga)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Prueba del torneo", "Sal del modo Play antes de lanzar la prueba.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:TournamentConfig");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Prueba del torneo", "No hay ningún TournamentConfig en el proyecto.", "OK");
            return;
        }
        TournamentConfig config = AssetDatabase.LoadAssetAtPath<TournamentConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        string configError;
        if (!config.IsValid(out configError))
        {
            EditorUtility.DisplayDialog("Prueba del torneo", config.name + ": " + configError, "OK");
            return;
        }

        // Copia de seguridad del guardado real
        string key = TournamentSession.SaveKeyName;
        bool hadSave = PlayerPrefs.HasKey(key);
        string backup = PlayerPrefs.GetString(key, "");

        List<string> errors = new List<string>();
        int matches = 0, reloads = 0, titles = 0;
        System.Random rnd = new System.Random(12345);

        try
        {
            for (int run = 0; run < Runs && errors.Count < 10; run++)
            {
                EditorUtility.DisplayProgressBar("Prueba del torneo", $"Torneo {run + 1} de {Runs}", run / (float)Runs);
                RunOne(config, rnd, errors, run, ref matches, ref reloads, ref titles);
            }
        }
        catch (System.Exception e)
        {
            errors.Add("Excepción: " + e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            TournamentSession.ForgetInMemory();
            if (hadSave) PlayerPrefs.SetString(key, backup);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        string summary = $"{Runs} torneos · {matches} partidos del jugador · {reloads} recargas · " +
                         $"{titles} títulos del jugador";
        if (errors.Count == 0)
        {
            Debug.Log("[Prueba del torneo] TODO CORRECTO. " + summary);
            EditorUtility.DisplayDialog("Prueba del torneo", "Todo correcto.\n\n" + summary, "OK");
        }
        else
        {
            StringBuilder sb = new StringBuilder();
            foreach (string e in errors) sb.AppendLine("• " + e);
            Debug.LogError("[Prueba del torneo] " + errors.Count + " errores:\n" + sb);
            EditorUtility.DisplayDialog("Prueba del torneo", errors.Count + " errores (detalle en la consola):\n\n" +
                                        errors[0], "OK");
        }
    }

    static void RunOne(TournamentConfig config, System.Random rnd, List<string> errors, int run,
                       ref int matches, ref int reloads, ref int titles)
    {
        string tag = "Torneo " + (run + 1) + ": ";

        TournamentSession.ForgetInMemory();
        TournamentSession.ClearSave();
        if (TournamentSession.GetSaveState(config) != TournamentSession.SaveState.None)
            errors.Add(tag + "después de borrar, el guardado no está vacío");

        if (!TournamentSession.StartNew(config)) { errors.Add(tag + "StartNew ha fallado"); return; }
        if (TournamentSession.GetSaveState(config) != TournamentSession.SaveState.InProgress)
            errors.Add(tag + "al empezar, el guardado no está 'en curso'");

        int player = TournamentSession.PlayerTeam;

        for (int step = 0; step < 100; step++)
        {
            BracketMatch next = TournamentSession.AdvanceToPlayerMatch();
            if (TournamentSession.Bracket.IsFinished) break;
            if (next == null) { errors.Add(tag + "no ha terminado pero no hay partido del jugador"); return; }

            // Mejoras pendientes (en el juego se eligen en la pantalla de resultado)
            for (int guard = 0; guard < 20 && TournamentSession.PlayerPicksPending > 0; guard++)
            {
                IReadOnlyList<int> offer = TournamentSession.PlayerOffer;
                if (offer.Count == 0) break;
                TournamentSession.ChoosePlayerUpgrade(rnd.Next(offer.Count));
            }
            if (TournamentSession.PlayerPicksPending != 0)
                errors.Add(tag + "quedan mejoras por elegir antes de jugar");

            // Cerrar y volver a abrir: todo debe seguir igual
            if (!Reload(config, errors, tag + "antes del partido " + (step + 1), next.id)) return;
            reloads++;

            int a = rnd.Next(0, 6), b = rnd.Next(0, 6);
            if (a == b) { if (rnd.Next(2) == 0) a++; else b++; }   // gol de oro
            if (!TournamentSession.ReportPlayerMatch(a, b)) { errors.Add(tag + "no se ha podido registrar el resultado"); return; }
            matches++;

            if (!Reload(config, errors, tag + "después del partido " + (step + 1), -1)) return;
            reloads++;
        }

        DoubleEliminationBracket br = TournamentSession.Bracket;
        if (!br.IsFinished) { errors.Add(tag + "el torneo no ha terminado en 100 pasos"); return; }
        if (TournamentSession.GetSaveState(config) != TournamentSession.SaveState.Finished)
            errors.Add(tag + "al terminar, el guardado no está como 'terminado'");
        if (TournamentSession.HasSave(config))
            errors.Add(tag + "un torneo terminado no debería poder continuarse");

        for (int t = 0; t < config.Teams.Count; t++)
        {
            int losses = br.Losses(t);
            if (losses > 2) errors.Add(tag + config.Teams[t].TeamName + " tiene " + losses + " derrotas");
            if (t == br.championTeam && losses > 1) errors.Add(tag + "el campeón tiene " + losses + " derrotas");
            if (t != br.championTeam && losses != 2)
                errors.Add(tag + config.Teams[t].TeamName + " acaba con " + losses + " derrotas (deberían ser 2)");
        }
        if (br.championTeam == player) titles++;
    }

    // Olvida el torneo y lo recupera del guardado. Comprueba que el cuadro y las mejoras son idénticos.
    static bool Reload(TournamentConfig config, List<string> errors, string where, int expectedMatchId)
    {
        string bracketBefore = JsonUtility.ToJson(TournamentSession.Bracket);
        string upgradesBefore = string.Join(",", TournamentSession.UpgradeIdsOf(TournamentSession.PlayerTeam));
        int playerBefore = TournamentSession.PlayerTeam;
        bool finished = TournamentSession.Bracket.IsFinished;

        TournamentSession.ForgetInMemory();
        if (TournamentSession.IsActive) { errors.Add(where + ": ForgetInMemory no ha olvidado el torneo"); return false; }

        if (finished)
        {
            // Un torneo terminado no se continúa: se comprueba el estado y se carga a mano para seguir
            if (TournamentSession.GetSaveState(config) != TournamentSession.SaveState.Finished)
            {
                errors.Add(where + ": el guardado del torneo terminado no se reconoce como terminado");
                return false;
            }
            return ResumeForTest(config, errors, where);
        }

        if (!TournamentSession.TryResume(config)) { errors.Add(where + ": TryResume ha fallado"); return false; }

        if (JsonUtility.ToJson(TournamentSession.Bracket) != bracketBefore)
            errors.Add(where + ": el cuadro cargado no coincide con el guardado");
        if (string.Join(",", TournamentSession.UpgradeIdsOf(TournamentSession.PlayerTeam)) != upgradesBefore)
            errors.Add(where + ": las mejoras del jugador no coinciden tras cargar");
        if (TournamentSession.PlayerTeam != playerBefore)
            errors.Add(where + ": el equipo del jugador ha cambiado tras cargar");
        if (expectedMatchId >= 0)
        {
            BracketMatch pending = TournamentSession.PendingPlayerMatch();
            if (pending == null || pending.id != expectedMatchId)
                errors.Add(where + ": tras cargar, el partido pendiente no es el mismo");
        }
        return true;
    }

    // TryResume no carga torneos terminados (es lo correcto para el menú). Para seguir comprobando
    // la cadena de pruebas basta con que el estado sea correcto: se da por bueno sin recargar.
    static bool ResumeForTest(TournamentConfig config, List<string> errors, string where)
    {
        if (TournamentSession.TryResume(config))
        {
            errors.Add(where + ": TryResume ha cargado un torneo ya terminado");
            return false;
        }
        // Volver a dejar el torneo terminado en memoria para las comprobaciones finales
        return RestoreFinished(config, errors, where);
    }

    static bool RestoreFinished(TournamentConfig config, List<string> errors, string where)
    {
        if (!TournamentSession.LoadFinishedForTests(config))
        {
            errors.Add(where + ": no se ha podido leer el torneo terminado");
            return false;
        }
        return true;
    }
}
#endif
