using System;
using UnityEngine;

namespace Tests.PlayMode.TerminalField
{
    public sealed class TerminalFieldContactProbe : MonoBehaviour
    {
        public Action<string> Emit;
        public int Step;

        private void OnCollisionEnter(Collision collision) => Record("enter", collision);
        private void OnCollisionStay(Collision collision) => Record("stay", collision);

        private void Record(string kind, Collision collision)
        {
            var other = collision.collider;
            Emit(FormattableString.Invariant($"{Step},{kind},{other.name},{other.gameObject.layer},{collision.contactCount},{collision.impulse.magnitude},{collision.relativeVelocity.magnitude},{transform.position.x},{transform.position.y},{other.transform.position.x},{other.transform.position.y}"));
        }
    }
}
