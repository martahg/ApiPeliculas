using ApiPeliculas.Data;
using ApiPeliculas.Modelos;
using ApiPeliculas.Modelos.Dtos;
using ApiPeliculas.Repositorio.IRepositorio;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using XSystem.Security.Cryptography;

namespace ApiPeliculas.Repositorio
{
    public class UsuarioRepositorio : IUsuarioRepositorio
    {
        private readonly ApplicationDbContext _bd;
        private string claveSecreta;
        private readonly UserManager<AppUsuario> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;

        public UsuarioRepositorio(ApplicationDbContext bd, IConfiguration config,
             UserManager<AppUsuario> userManager, RoleManager<IdentityRole> roleManager,
             IMapper mapper) 
        {
            _bd = bd;
            claveSecreta = config.GetValue<string>("ApiSettings:Secreta");
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
        }


        public AppUsuario GetUsuario(string usuarioId)
        {
            return _bd.AppUsuario.FirstOrDefault(u => u.Id == usuarioId);
        }

        public ICollection<AppUsuario> GetUsuarios()
        {
            return _bd.AppUsuario.OrderBy(u => u.UserName).ToList();
        }

        public bool IsUniqueUser(string usuario)
        {
            var usuariobd = _bd.AppUsuario.FirstOrDefault(u => u.UserName == usuario);
            if (usuariobd == null)
            {
                return true;
            }

            return false;
        }

        public async Task<UsuarioDatosDto> Registro(UsuarioRegistroDto usuarioRegistroDto)
        {   
            AppUsuario usuario = new AppUsuario()
            {
                UserName = usuarioRegistroDto.NombreUsuario,
                Email = usuarioRegistroDto.NombreUsuario,
                NormalizedEmail = usuarioRegistroDto.NombreUsuario.ToUpper(),
                Nombre = usuarioRegistroDto.Nombre
            };

            var result = await _userManager.CreateAsync(usuario, usuarioRegistroDto.Password);
            if (result.Succeeded)
            {
                // Solo la primera vez y es para crear los roles
                if (!_roleManager.RoleExistsAsync("admin").GetAwaiter().GetResult()) 
                {
                    await _roleManager.CreateAsync(new IdentityRole("admin"));
                    await _roleManager.CreateAsync(new IdentityRole("registrado"));
                }

                await _userManager.AddToRoleAsync(usuario, "admin");
                var usuarioRetornado = _bd.AppUsuario.FirstOrDefault(u => u.UserName == usuarioRegistroDto.NombreUsuario);

                return _mapper.Map<UsuarioDatosDto>(usuarioRetornado);
            }

            return new UsuarioDatosDto();
        }

        public async Task<UsuarioLoginRespuestaDto> Login(UsuarioLoginDto usuarioLoginDto)
        {
            var usuario = _bd.AppUsuario.FirstOrDefault(
                u => u.UserName.ToLower() == usuarioLoginDto.NombreUsuario.ToLower());


            bool isValida = await _userManager.CheckPasswordAsync(usuario, usuarioLoginDto.Password);

            // Validamos si el usuario no existe con la combinación de usuario y contraseña correcta.
            if (usuario == null || isValida == false)
            {
                return new UsuarioLoginRespuestaDto()
                {
                    Token = "",
                    usuario = null
                };
            }

            // Aqui si existe el usuario entonces podemos procesar el login
            var roles = await _userManager.GetRolesAsync(usuario);

            var manejadorToken = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(claveSecreta);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, usuario.UserName.ToString()),
                    new Claim(ClaimTypes.Role, roles.FirstOrDefault())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = manejadorToken.CreateToken(tokenDescriptor);

            UsuarioLoginRespuestaDto usuarioLoginRespuestaDto = new UsuarioLoginRespuestaDto()
            {
                Token = manejadorToken.WriteToken(token),
                usuario = _mapper.Map<UsuarioDatosDto>(usuario)
            };

            return usuarioLoginRespuestaDto;
        }

        //public async Task<Usuario> Registro(UsuarioRegistroDto usuarioRegistroDto)
        //{
        //    var passwordEncriptado = obtenermd5(usuarioRegistroDto.Password);

        //    var usuario = new Usuario()
        //    {
        //        NombreUsuario = usuarioRegistroDto.NombreUsuario,
        //        Password = passwordEncriptado,
        //        Nombre = usuarioRegistroDto.Nombre,
        //        Role = usuarioRegistroDto.Role
        //    };

        //    _bd.Usuario.Add(usuario);
        //    await _bd.SaveChangesAsync();
        //    usuario.Password = passwordEncriptado;
        //    return usuario;
        //}

        //public async Task<UsuarioLoginRespuestaDto> Login(UsuarioLoginDto usuarioLoginDto)
        //{
        //    var passwordEncriptado = obtenermd5(usuarioLoginDto.Password);

        //    var usuario = _bd.AppUsuario.FirstOrDefault(
        //        u => u.NombreUsuario.ToLower() == usuarioLoginDto.NombreUsuario.ToLower()
        //        && u.Password == passwordEncriptado
        //        );

        //    // Validamos si el usuario no existe con la combinación de usuario y contraseña correcta.
        //    if (usuario == null)
        //    {
        //        return new UsuarioLoginRespuestaDto()
        //        {
        //            Token = "",
        //            usuario = null
        //        };
        //    }

        //    // Aqui si existe el usuario entonces podemos procesar el login
        //    var manejadorToken = new JwtSecurityTokenHandler();
        //    var key = Encoding.ASCII.GetBytes(claveSecreta);

        //    var tokenDescriptor = new SecurityTokenDescriptor
        //    {
        //        Subject = new ClaimsIdentity(new Claim[]
        //        {
        //            new Claim(ClaimTypes.Name, usuario.NombreUsuario.ToString()),
        //            new Claim(ClaimTypes.Role, usuario.Role)
        //        }),
        //        Expires = DateTime.UtcNow.AddDays(7),
        //        SigningCredentials = new(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        //    };

        //    var token = manejadorToken.CreateToken(tokenDescriptor);

        //    UsuarioLoginRespuestaDto usuarioLoginRespuestaDto = new UsuarioLoginRespuestaDto()
        //    {
        //        Token = manejadorToken.WriteToken(token),
        //        usuario = usuario
        //    };

        //    return usuarioLoginRespuestaDto;
        //}

        //Método para encriptar contraseña con MD5 se usa tanto en el Acceso como en el Registro (esto deberia ir en una carpeta helpers)
        //public static string obtenermd5(string valor)
        //{
        //    MD5CryptoServiceProvider x = new MD5CryptoServiceProvider();
        //    byte[] data = System.Text.Encoding.UTF8.GetBytes(valor);
        //    data = x.ComputeHash(data);
        //    string resp = "";
        //    for (int i = 0; i < data.Length; i++)
        //        resp += data[i].ToString("x2").ToLower();
        //    return resp;
        //}
    }
}
