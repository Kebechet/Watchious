import Foundation
import Security

// The Keychain behind ISecureStorage: one generic password of the service "watchious" per key.
private let secureStorageService = "watchious"

private func secureStorageQuery(_ key: String?) -> [String: Any] {
    var query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrService as String: secureStorageService,
    ]
    if let key {
        query[kSecAttrAccount as String] = key
    }
    return query
}

// The caller frees the string; nil when there is no value.
@_cdecl("watchious_secure_get")
public func watchiousSecureGet(_ key: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar>? {
    var query = secureStorageQuery(String(cString: key))
    query[kSecReturnData as String] = true
    query[kSecMatchLimit as String] = kSecMatchLimitOne
    var result: AnyObject?
    guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
          let data = result as? Data,
          let value = String(data: data, encoding: .utf8) else {
        return nil
    }
    return strdup(value)
}

// The Keychain's status: 0 when the value is stored.
@_cdecl("watchious_secure_set")
public func watchiousSecureSet(_ key: UnsafePointer<CChar>, _ value: UnsafePointer<CChar>) -> Int32 {
    let query = secureStorageQuery(String(cString: key))
    let data = Data(String(cString: value).utf8)
    let updated = SecItemUpdate(query as CFDictionary, [kSecValueData as String: data] as CFDictionary)
    guard updated == errSecItemNotFound else {
        return updated
    }
    var item = query
    item[kSecValueData as String] = data
    item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlock
    return SecItemAdd(item as CFDictionary, nil)
}

// 1 when there was a value to remove.
@_cdecl("watchious_secure_remove")
public func watchiousSecureRemove(_ key: UnsafePointer<CChar>) -> Int32 {
    SecItemDelete(secureStorageQuery(String(cString: key)) as CFDictionary) == errSecSuccess ? 1 : 0
}

@_cdecl("watchious_secure_remove_all")
public func watchiousSecureRemoveAll() {
    SecItemDelete(secureStorageQuery(nil) as CFDictionary)
}
