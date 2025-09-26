using UnityEngine;

using Veyu;

public class VeyuExample : MonoBehaviour
{
    void Start()
    {
        VeyuSdk.Init();
        VeyuSdk.LogSystem("Wow Veyu is so cool :) !");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            VeyuSdk.LogInput("The player jumped !");
        }
    }

    private async void OnApplicationQuit()
    {
        await VeyuSdk.Save();
        // or upload to cloud with
        // await VeyuSdk.Upload();
    }
}
