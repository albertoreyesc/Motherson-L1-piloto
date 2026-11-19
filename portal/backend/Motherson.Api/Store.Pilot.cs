using Dapper;
using Microsoft.AspNetCore.Identity;
namespace Motherson;

public sealed partial class Store
{
    sealed record UsuarioPiloto(int Id,string Usuario,string Nombre,string Rol,int? Puesto,string Hash);
    const string UsuariosPiloto="SELECT id_usuario Id,usuario Usuario,nombre Nombre,rol Rol,id_puesto Puesto,password_hash Hash FROM mes.USUARIO_PILOTO WHERE activo=1";
    static Cuenta CuentaPiloto(UsuarioPiloto u)=>new(u.Id,u.Usuario,u.Nombre,u.Rol=="ADMIN"?"ADMINISTRADOR":u.Rol=="PUESTO"?"PUESTO":"LECTURA",u.Rol=="ADMIN"?"ADMIN_PUESTOS":u.Rol=="PUESTO"?"PUESTO":"LECTOR_PILOTO",u.Puesto){SelloAcceso=Sello(u.Usuario,u.Hash),Piloto=true};
    async Task<Cuenta?> LoginPiloto(LoginInput input)
    {
        await using var c=await Open();
        var u=await c.QuerySingleOrDefaultAsync<UsuarioPiloto>(UsuariosPiloto+" AND usuario=@usuario",new{usuario=input.Username});
        var valid=Hasher.VerifyHashedPassword("",u?.Hash??Dummy,input.Password)!=PasswordVerificationResult.Failed;
        return u is not null&&valid?CuentaPiloto(u):null;
    }
    async Task<Cuenta> ActorPiloto(int id,string tipo,string? sello)
    {
        await using var c=await Open();
        var u=await c.QuerySingleOrDefaultAsync<UsuarioPiloto>(UsuariosPiloto+" AND id_usuario=@id",new{id});
        var actor=u is null?null:CuentaPiloto(u);
        return actor is not null&&actor.TipoCuenta==tipo&&actor.SelloAcceso==sello?actor:throw new RuleException("La sesión venció o el acceso cambió.",401);
    }
}
