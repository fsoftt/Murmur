import json, sys
from noise.connection import NoiseConnection, Keypair
from cryptography.hazmat.primitives.asymmetric.x25519 import X25519PrivateKey
from cryptography.hazmat.primitives import serialization

def priv(seed):
    return bytes([(seed + i) % 256 for i in range(32)])
def pub(p):
    return X25519PrivateKey.from_private_bytes(p).public_key().public_bytes(serialization.Encoding.Raw, serialization.PublicFormat.Raw)

vectors = []
for pattern in ["KK", "IK"]:
    name = f"Noise_{pattern}_25519_ChaChaPoly_SHA256"
    i_s, r_s, i_e, r_e = priv(1), priv(50), priv(100), priv(150)
    prologue = b"Murmur test prologue"
    ini = NoiseConnection.from_name(name.encode())
    res = NoiseConnection.from_name(name.encode())
    ini.set_as_initiator(); res.set_as_responder()
    for c in (ini, res): c.set_prologue(prologue)
    ini.set_keypair_from_private_bytes(Keypair.STATIC, i_s)
    res.set_keypair_from_private_bytes(Keypair.STATIC, r_s)
    ini.set_keypair_from_public_bytes(Keypair.REMOTE_STATIC, pub(r_s))
    if pattern == "KK":
        res.set_keypair_from_public_bytes(Keypair.REMOTE_STATIC, pub(i_s))
    ini.set_keypair_from_private_bytes(Keypair.EPHEMERAL, i_e)
    res.set_keypair_from_private_bytes(Keypair.EPHEMERAL, r_e)
    ini.start_handshake(); res.start_handshake()
    msgs = []
    p1 = b"first payload"; m1 = ini.write_message(p1); assert res.read_message(m1) == p1
    msgs.append({"payload": p1.hex(), "ciphertext": m1.hex()})
    p2 = b"second payload"; m2 = res.write_message(p2); assert ini.read_message(m2) == p2
    msgs.append({"payload": p2.hex(), "ciphertext": m2.hex()})
    assert ini.handshake_finished and res.handshake_finished
    for k, (a, b) in enumerate([(ini, res), (res, ini), (ini, res)]):
        p = f"transport {k}".encode(); m = a.encrypt(p); assert b.decrypt(m) == p
        msgs.append({"payload": p.hex(), "ciphertext": m.hex()})
    vectors.append({"protocol_name": name, "prologue": prologue.hex(),
        "init_static": i_s.hex(), "resp_static": r_s.hex(), "init_ephemeral": i_e.hex(), "resp_ephemeral": r_e.hex(),
        "handshake_hash": ini.get_handshake_hash().hex(), "messages": msgs})
json.dump({"vectors": vectors}, sys.stdout, indent=2)
