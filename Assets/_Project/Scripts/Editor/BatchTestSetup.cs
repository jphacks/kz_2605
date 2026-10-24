using System;
using UnityEditor;
using UnityEditor.XR.Management;

namespace JPHACKS2026.Editor
{
    /// <summary>HMDを使わないバッチテストだけでXRの自動起動を止める。設定ファイルは保存しない。</summary>
    [InitializeOnLoad]
    public static class BatchTestSetup
    {
        static BatchTestSetup()
        {
            if (!UnityEngine.Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-otogiNoXR") < 0) return;
            DisableXR();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) DisableXR();
            };
        }
        static void DisableXR()
        {
            var settings=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(settings!=null) settings.InitManagerOnStart=false;
        }
    }
}
