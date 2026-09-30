namespace Kiln.Core.Tests.Fixtures;

/// <summary>Types with mixed visibility that the tests read back from this test assembly's metadata.</summary>
public static class ApiSurfaceFixtures
{
    public interface IPublicContract
    {
        string Name { get; }

        void Execute();
    }

    public enum PublicMode
    {
        First,
        Second,
    }

    public sealed record PublicRecord(string Value);

    public struct PublicStruct
    {
        public int Size;
    }

    public delegate void PublicHandler();

    public static class PublicStatic
    {
        public static void Helper()
        {
        }
    }

    public class PublicClass
    {
        private int _secret;

        public void Visible() => _secret++;

        protected virtual void Hook()
        {
        }

        internal void HiddenMethod() => _secret--;

        public class NestedPublic
        {
            public void Ping()
            {
            }
        }

        internal class NestedInternal
        {
            public void Pong()
            {
            }
        }
    }

    public sealed class SealedClass : PublicClass
    {
        protected override void Hook()
        {
        }
    }

    public class GenericBox<T>
    {
        public T? Value { get; set; }

        public TResult Map<TResult>(Func<T?, TResult> selector) => selector(Value);
    }

    internal class HiddenType
    {
        public void Reveal()
        {
        }

        public class InnerOfHidden
        {
            public void Inside()
            {
            }
        }
    }
}
