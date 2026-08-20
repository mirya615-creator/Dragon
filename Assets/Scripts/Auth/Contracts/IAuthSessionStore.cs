public interface IAuthSessionStore
{
    AuthSession Current { get; }
    bool TryRestore(out AuthSession session);
    bool IsValid(AuthSession session);
    void Set(AuthSession session);
    void Clear();
}
