namespace Flow.Runtime;

public sealed class WorkflowAssertionException : Exception
{
    public string FailureCode { get; }

    public WorkflowAssertionException(string failureCode) : base(failureCode) => FailureCode = failureCode;
}
