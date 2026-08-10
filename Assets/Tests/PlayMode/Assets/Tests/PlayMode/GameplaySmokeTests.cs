using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TicTacToeRoguelike.PlayModeTests
{
    /// <summary>
    /// Verifica o fluxo mínimo da cena real.
    ///
    /// Este não é um teste detalhado das regras. Ele protege a integração entre
    /// a cena, o EncounterController, o tabuleiro visual e a jogada da IA.
    /// </summary>
    public sealed class GameplaySmokeTests
    {
        [UnityTest]
        public IEnumerator GameplaySceneStartsAndAlternatesAfterPlayerMove()
        {
            AsyncOperation loading =
                SceneManager.LoadSceneAsync(
                    "Gameplay",
                    LoadSceneMode.Single);

            Assert.That(
                loading,
                Is.Not.Null,
                "Gameplay precisa estar na Scene List do Build Profile.");

            yield return loading;

            /*
             * Start() é executado no primeiro frame depois que a cena termina
             * de carregar. Esse frame permite que o encontro padrão seja criado.
             */
            yield return null;

            EncounterController controller =
                Object.FindFirstObjectByType<EncounterController>();

            BoardView boardView =
                Object.FindFirstObjectByType<BoardView>();

            Assert.That(
                controller,
                Is.Not.Null,
                "A cena Gameplay precisa possuir um EncounterController.");

            Assert.That(
                boardView,
                Is.Not.Null,
                "A cena Gameplay precisa possuir uma BoardView.");

            Assert.That(
                controller.Board,
                Is.Not.Null,
                "O encontro padrão deveria criar um BoardState.");

            Assert.That(
                controller.Phase,
                Is.EqualTo(
                    EncounterController.EncounterPhase.WaitingForPlayer),
                "O encontro deveria começar esperando a jogada do Player.");

            BoardCellView firstCell = null;

            foreach (BoardCellView cell in boardView.CellViews.Values)
            {
                firstCell = cell;
                break;
            }

            Assert.That(
                firstCell,
                Is.Not.Null,
                "O tabuleiro visual deveria possuir pelo menos uma casa.");

            long versionBeforeMove =
                controller.Board.Version;

            /*
             * Chamamos a mesma entrada usada pelo clique real. A BoardCellView
             * emite o evento, a BoardView o encaminha e o EncounterController
             * envia a jogada ao MoveService.
             */
            firstCell.OnPointerClick(null);

            /*
             * Esperamos duas alterações:
             * 1. a jogada do Player;
             * 2. a resposta da IA.
             *
             * O limite impede que um defeito transforme o teste em espera infinita.
             */
            float timeout =
                Time.realtimeSinceStartup + 10f;

            while (
                controller.Board != null &&
                controller.Board.Version < versionBeforeMove + 2 &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                controller.Board.Version,
                Is.GreaterThanOrEqualTo(versionBeforeMove + 2),
                "Player e IA deveriam aplicar uma jogada cada.");

            Assert.That(
                controller.CurrentSide,
                Is.EqualTo(EncounterController.EncounterSide.Player),
                "Depois da resposta da IA, o controle deveria voltar ao Player.");

            Assert.That(
                controller.Phase,
                Is.EqualTo(
                    EncounterController.EncounterPhase.WaitingForPlayer),
                "O input deveria voltar a ficar disponível para o Player.");

            LogAssert.NoUnexpectedReceived();
        }
    }
}