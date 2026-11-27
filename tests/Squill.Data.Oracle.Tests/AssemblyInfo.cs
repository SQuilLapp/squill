using Xunit;

// Each class fixture starts a heavy container and sets a process-wide environment variable, so classes run one at a time.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
