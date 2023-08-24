using Xunit;

namespace Client_Core_Tests
{
    /// <summary>
    /// Associate tests with this collection to ensure they do not run in parallel with other test collections.
    /// e.g. if your test modifies Environment.CurrentDirectory, you MUST set your collection to not be run in parallel
    /// or you will cause other tests to potentially error out.
    /// </summary>
    [CollectionDefinition("NonParallelCollection", DisableParallelization = true)]
    public class NonParallelCollectionDefinition
    {
    }
}
