"""Saca dev_flash/sys/external/libsre.sprx de un PS3UPDAT.PUP (firmware oficial del PS3).

Licencia: GPL-2.0 (ver COPYING en esta carpeta), como unself.py: sigue el formato PUP y
el SCEDecrypter de RPCS3 (https://github.com/RPCS3/rpcs3). Programa aparte, llamado por
gerar.ps1. Las claves (SCEPKG_ERK / SCEPKG_RIV) se leen del key_vault.h de RPCS3 que
unself.py descarga; no están en este archivo.

El PUP del propio disco del juego (PS3_GAME/../PS3_UPDATE/PS3UPDAT.PUP) sirve: así no
hace falta instalar el firmware en RPCS3.

uso:
  python pup_libsre.py PS3UPDAT.PUP libsre.sprx --vault PASTA [--baixar]
"""
import argparse
import io
import re
import struct
import sys
import tarfile
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from unself import AES, Counter, baixar_vault  # noqa: E402

ALVO = 'sys/external/libsre.sprx'


def be(fmt, buf, off):
    return struct.unpack_from('>' + fmt, buf, off)


def chaves_pkg(pasta):
    h = (Path(pasta) / 'key_vault.h').read_text(encoding='utf-8', errors='replace')

    def array(nome):
        corpo = re.search(r'constexpr u8 ' + nome + r'\[[^\]]*\]\s*=\s*\{([^}]*)\}', h).group(1)
        return bytes(int(x, 16) for x in re.findall(r'0x[0-9A-Fa-f]{2}', corpo))
    return array('SCEPKG_ERK'), array('SCEPKG_RIV')


def entradas_pup(pup):
    """{id: bytes} de las entradas del PUP (0x300 = update_files.tar)."""
    if pup[:5] != b'SCEUF':
        raise SystemExit('no es un PUP (magic SCEUF)')
    _pkgv, _imgv, n, _hlen, _dlen = be('QQQQQ', pup, 8)
    out = {}
    for i in range(n):
        eid, off, tam, _pad = be('QQQQ', pup, 0x30 + i * 0x20)
        out[eid] = pup[off:off + tam]
    return out


def descifrar_sce(dados, erk, riv):
    """Un contenedor SCE de categoria paquete (los dev_flash_*.tar.aa del PUP): la lista de
    secciones de datos descifradas (y descomprimidas). El tar es una de ellas (la tercera
    en RPCS3)."""
    magic, _hver, _flags, _tipo, se_meta, se_hsize, _esize = be('IIHHIQQ', dados, 0)
    if magic != 0x53434500:
        raise ValueError('no es SCE')
    mi = AES.new(erk, AES.MODE_CBC, iv=riv).decrypt(dados[se_meta + 0x20:se_meta + 0x60])
    key, key_pad, iv, iv_pad = mi[:16], mi[16:32], mi[32:48], mi[48:64]
    if key_pad[0] != 0 or iv_pad[0] != 0:
        raise ValueError('no se pudo descifrar el metadata info')
    ini = se_meta + 0x60
    hdrs = AES.new(key, AES.MODE_CTR, counter=Counter.new(128, initial_value=int.from_bytes(iv, 'big'))).decrypt(dados[ini:se_hsize])
    _sig_len, _u1, nsec, nkeys, _opt, _u2, _u3 = be('QIIIIII', hdrs, 0)
    secs = [be('QQIIIIIIII', hdrs, 0x20 + i * 0x30) for i in range(nsec)]
    kbase = 0x20 + nsec * 0x30
    claves = hdrs[kbase:kbase + nkeys * 16]
    salida = []
    for (d_off, d_size, _t, _p, _h, _s, cifrado, kidx, ividx, comprimido) in secs:
        bloque = dados[d_off:d_off + d_size]
        if cifrado == 3:
            k = claves[kidx * 16:kidx * 16 + 16]
            v = claves[ividx * 16:ividx * 16 + 16]
            bloque = AES.new(k, AES.MODE_CTR, counter=Counter.new(128, initial_value=int.from_bytes(v, 'big'))).decrypt(bloque)
        if comprimido == 2:
            bloque = zlib.decompress(bloque)
        salida.append(bytes(bloque))
    return salida


def libsre_de_pup(pup, erk, riv):
    ent = entradas_pup(pup)
    if 0x300 not in ent:
        raise SystemExit('el PUP no trae update_files.tar (entrada 0x300)')
    with tarfile.open(fileobj=io.BytesIO(ent[0x300])) as tar:
        for m in tar.getmembers():
            if 'dev_flash_' not in m.name:
                continue
            datos = tar.extractfile(m).read()
            try:
                secciones = descifrar_sce(datos, erk, riv)
            except ValueError:
                continue
            for plano in secciones:
                try:
                    with tarfile.open(fileobj=io.BytesIO(plano)) as t2:
                        for m2 in t2.getmembers():
                            if m2.name.replace('\\', '/').endswith(ALVO):
                                return t2.extractfile(m2).read(), m.name
                except tarfile.TarError:
                    continue
    raise SystemExit('libsre.sprx no está en este PUP')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('pup')
    ap.add_argument('saida')
    ap.add_argument('--vault', required=True, help='carpeta con key_vault.cpp y key_vault.h de RPCS3')
    ap.add_argument('--baixar', action='store_true', help='descarga los dos archivos de RPCS3 si faltan')
    a = ap.parse_args()
    if a.baixar:
        baixar_vault(a.vault)
    erk, riv = chaves_pkg(a.vault)
    datos, origen = libsre_de_pup(Path(a.pup).read_bytes(), erk, riv)
    Path(a.saida).write_bytes(datos)
    print(f'ok: libsre.sprx ({len(datos)} bytes) de {origen} -> {a.saida}')


if __name__ == '__main__':
    main()
