using UnityEditor;
using UnityEngine;

namespace Sandbox.Editor
{
    /// <summary>
    /// Forwards the old command-line entry to <see cref="SampleSceneBuilder"/>.
    /// </summary>
    public static class SandboxLabSceneBuilder
    {
        public static void RebuildFromCommandLine()
        {
            SampleSceneBuilder.RebuildFromCommandLine();
        }
    }
}
