using Xunit;

// The Docker fixture starts one heavy container and sets a process-wide environment variable, so classes run one at a time.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
