// watchOS has no GSS.framework, but .NET's System.Net.Security.Native, which every app that uses HttpClient or
// ClientWebSocket links, calls it for Kerberos and NTLM (Negotiate) authentication, and the link fails without it.
// These stand-ins report GSS as unavailable (GSS_S_UNAVAILABLE, RFC 2744), so .NET treats Negotiate authentication
// as not supported, as on platforms without GSS.
#include <stddef.h>
#include <stdint.h>

typedef uint32_t OM_uint32;

typedef struct {
    OM_uint32 length;
    void *elements;
} watchious_gss_oid_desc;

#define WATCHIOUS_GSS_S_UNAVAILABLE ((OM_uint32)16 << 16)

watchious_gss_oid_desc __gss_c_nt_hostbased_service_oid_desc = { 0, NULL };
watchious_gss_oid_desc __gss_c_nt_user_name_oid_desc = { 0, NULL };
watchious_gss_oid_desc __gss_krb5_mechanism_oid_desc = { 0, NULL };
watchious_gss_oid_desc __gss_ntlm_mechanism_oid_desc = { 0, NULL };
watchious_gss_oid_desc __gss_spnego_mechanism_oid_desc = { 0, NULL };

static OM_uint32 watchious_gss_unavailable(OM_uint32 *minor_status)
{
    if (minor_status != NULL) {
        *minor_status = 0;
    }
    return WATCHIOUS_GSS_S_UNAVAILABLE;
}

// Every GSS routine below takes the minor status first; the stand-ins read only that, and the arm64 calling
// convention passes it in the same register whatever the routine's other parameters are.
OM_uint32 gss_accept_sec_context(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_acquire_cred(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_acquire_cred_with_password(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_delete_sec_context(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_display_status(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_get_mic(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_import_name(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_indicate_mechs(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_init_sec_context(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_release_buffer(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_release_cred(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_release_name(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_release_oid_set(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }
OM_uint32 gss_verify_mic(OM_uint32 *minor_status) { return watchious_gss_unavailable(minor_status); }

int gss_oid_equal(const watchious_gss_oid_desc *first, const watchious_gss_oid_desc *second)
{
    return first == second;
}
