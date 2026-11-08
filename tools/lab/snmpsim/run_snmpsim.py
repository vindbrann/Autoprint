"""Lance snmpsim (labo Autoprint) : les requêtes SNMPv3 sans nom de contexte (cas des vraies imprimantes et
d'Autoprint) sont servies par le fichier de données « public.snmprec » au lieu d'être refusées."""
import sys
from pyasn1.type import univ
from pysnmp.entity.rfc3413 import context

_orig = context.SnmpContext.get_mib_instrum

def _get_mib_instrum(self, contextName=b""):
    name = univ.OctetString(contextName).asOctets()
    if name == b"" and b"" not in self.context_names and b"public" in self.context_names:
        name = b"public"
    return _orig(self, name)

context.SnmpContext.get_mib_instrum = _get_mib_instrum

from snmpsim.commands.responder import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
