namespace IdermaFichas.Helpers;

public interface IEdicionProducto
{
    bool HayCambiosPendientes { get; }

    bool AutoguardarSiEsPosible();

    Task<bool> ConfirmarSalidaAsync();
}
