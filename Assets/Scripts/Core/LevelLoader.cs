using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Blokfit.Generation;
using Blokfit.ScriptableObjects;

namespace Blokfit.Core
{
    public class LevelLoader : MonoBehaviour
    {
        [SerializeField] private string         _remoteUrlTemplate = "";
        [SerializeField] private LevelGenerator _levelGenerator;

        public IEnumerator FetchOrGenerate(DifficultyConfig config, Action<LevelData> onComplete)
        {
            if (!string.IsNullOrEmpty(_remoteUrlTemplate))
            {
                string url = string.Format(_remoteUrlTemplate, config.difficultyName.ToLower());
                using var request = UnityWebRequest.Get(url);
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    LevelData remote = TryParse(request.downloadHandler.text);
                    if (remote != null)
                    {
                        onComplete(remote);
                        yield break;
                    }
                }
            }

            string resourceKey = $"Levels/level_{config.difficultyName.ToLower()}";
            var textAsset = Resources.Load<TextAsset>(resourceKey);
            if (textAsset != null)
            {
                LevelData local = TryParse(textAsset.text);
                if (local != null)
                {
                    onComplete(local);
                    yield break;
                }
            }

            onComplete(_levelGenerator.Generate(config));
        }

        private LevelData TryParse(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = JsonUtility.FromJson<LevelData>(json);
                return (data?.pieces != null && data.pieces.Length > 0) ? data : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
