using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace fzmnm.Test
{
    public class Rotate : MonoBehaviour
    {
        public Vector3 angularVelocityDegree;
        private void Update()
        {
            transform.rotation = Quaternion.AngleAxis(angularVelocityDegree.magnitude * Time.deltaTime, angularVelocityDegree.normalized) * transform.rotation;
        }
    }

}

