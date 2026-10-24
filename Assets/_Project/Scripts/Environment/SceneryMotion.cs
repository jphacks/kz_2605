using UnityEngine;
namespace JPHACKS2026
{
    public class SceneryMotion : MonoBehaviour
    {
        public float phase, amplitude = 8;
        public bool wave;
        Vector3 position;
        Quaternion rotation;
        void Start() { position=transform.localPosition; rotation=transform.localRotation; }
        void Update()
        {
            float a=Mathf.Sin(Time.time*(wave?.65f:2.4f)+phase)*amplitude;
            if(wave) transform.localPosition=position+new Vector3(0,0,a);
            else transform.localRotation=rotation*Quaternion.Euler(0,a*.4f,a);
        }
    }
}
