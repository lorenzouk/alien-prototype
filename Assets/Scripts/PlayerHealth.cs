using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : Health
{
    //override death func
    public override void Death()
    {
        print("You died!!!!!!!!!!!!!!!!");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
