import 'legal_document.dart';

/// Placeholder legal copy for Purch.io's Privacy Policy and Terms of
/// Service. Written to be accurate to what the product actually does
/// (offline-first local storage, background sync, BIR-related business
/// data, per-tenant branding) so it's a reasonable starting draft, but it
/// has NOT been reviewed by counsel — replace before a real launch.
abstract class LegalContent {
  static const String lastUpdated = 'This draft has not yet been dated —'
      ' set the effective date once legal review is complete.';

  static const List<LegalSection> privacyPolicy = [
    LegalSection(
      heading: '1. What this policy covers',
      body:
          'This Privacy Policy describes how Purch.io ("the App", "we") '
          'handles information when a business ("the Tenant") and its '
          'staff use the app to run point-of-sale, inventory, and '
          'reporting operations. It applies to data entered into the app '
          'and data the app generates while it runs, on tablets, kiosks, '
          'and any connected server.',
    ),
    LegalSection(
      heading: '2. Information we collect',
      body:
          '• Account & device data: staff names, PINs (stored hashed, '
          'never in plain text), device pairing codes, and branch '
          'assignments.\n'
          '• Business data: catalog items, prices, inventory levels, '
          'suppliers, and branch/department structure that the Tenant '
          'configures.\n'
          '• Transaction data: sales, payments, discounts (including '
          'Senior Citizen/PWD discount records as required by BIR '
          'regulations), refunds, and shift/cash-drawer activity.\n'
          '• Customer credit ledger data ("utang"): when a Tenant enables '
          'credit sales, the customer\'s name, contact details, and '
          'running balance are stored for as long as the Tenant\'s '
          'configured retention period.\n'
          '• Compliance data: TIN, registered business name and address, '
          'and BIR X/Z-reading sequences, used solely to generate '
          'BIR-compliant receipts and reports.\n'
          '• Device diagnostics: sync status, offline queue state, and '
          'error logs, used to keep the offline-first sync reliable.',
    ),
    LegalSection(
      heading: '3. How we use information',
      body:
          'Information is used to operate the point-of-sale and '
          'inventory system the Tenant configured: processing sales, '
          'tracking stock, generating BIR-compliant receipts and '
          'readings, producing sales/inventory reports, and keeping data '
          'in sync across a Tenant\'s branches and devices. We do not '
          'sell Tenant or customer data, and do not use transaction data '
          'to serve advertising.',
    ),
    LegalSection(
      heading: '4. Offline storage & synchronization',
      body:
          'Purch.io is offline-first: data is stored locally on each '
          'device (in an on-device database) so staff can keep selling '
          'during unstable connectivity, then synced to the Tenant\'s '
          'configured server (cloud-hosted or on-premise/local server) '
          'once connectivity returns. A Tenant that chooses a local/'
          'on-premise server deployment keeps that data on infrastructure '
          'it controls; we do not have access to data hosted on a '
          'Tenant\'s own local server.',
    ),
    LegalSection(
      heading: '5. Sharing of information',
      body:
          'We share information only: (a) within the Tenant\'s own '
          'organization, as configured by their staff roles and branch '
          'access; (b) with payment or e-wallet providers the Tenant '
          'chooses to integrate (e.g. GCash QR), limited to what\'s '
          'needed to complete a transaction; (c) with the Bureau of '
          'Internal Revenue (BIR) or other regulators, where the Tenant '
          'is legally required to report sales data; and (d) where '
          'required by law or to protect the rights, safety, or property '
          'of Purch.io, a Tenant, or others.',
    ),
    LegalSection(
      heading: '6. Data retention',
      body:
          'Transaction and compliance records are retained as long as '
          'required by Philippine tax law and the Tenant\'s own record-'
          'keeping obligations. Customer credit ledger records are kept '
          'for the retention period the Tenant configures in Business '
          'Settings. A Tenant may request deletion of data it controls, '
          'subject to any legal retention requirements that override '
          'that request.',
    ),
    LegalSection(
      heading: '7. Your rights',
      body:
          'Subject to applicable law (including the Philippine Data '
          'Privacy Act of 2012), individuals whose data is processed '
          'through the app — staff and, where applicable, credit-ledger '
          'customers — may request access to, correction of, or deletion '
          'of their personal data by contacting the Tenant business '
          'directly, as the Tenant is the data controller for information '
          'it collects through the app.',
    ),
    LegalSection(
      heading: '8. Security',
      body:
          'PINs are stored hashed, not in plain text. Sessions use secure '
          'token storage on-device. We apply reasonable technical and '
          'organizational safeguards, but no system is perfectly secure; '
          'Tenants are responsible for physical device security and '
          'restricting staff access appropriately.',
    ),
    LegalSection(
      heading: '9. Changes to this policy',
      body:
          'We may update this policy as the product evolves. Material '
          'changes will be reflected here with an updated effective date, '
          'and continued use of the app after a change constitutes '
          'acceptance of the revised policy.',
    ),
    LegalSection(
      heading: '10. Contact',
      body:
          'Questions about this policy can be directed to the business '
          'that operates this installation of Purch.io, or to the '
          'Purch.io team through the channel provided at setup.',
    ),
  ];

  static const List<LegalSection> termsOfService = [
    LegalSection(
      heading: '1. Acceptance of terms',
      body:
          'By setting up a business ("Tenant") or using a device paired '
          'to a Tenant in Purch.io, you agree to these Terms of Service '
          'and the accompanying Privacy Policy. If you do not agree, do '
          'not complete setup or use the app.',
    ),
    LegalSection(
      heading: '2. What Purch.io is',
      body:
          'Purch.io is a configurable, offline-capable point-of-sale and '
          'store-management system for Philippine small and medium '
          'businesses, covering sales, inventory, staff, and BIR-related '
          'compliance reporting across convenience stores, restaurants/'
          'cafés, grocery, retail, department stores, service '
          'establishments, and sari-sari stores.',
    ),
    LegalSection(
      heading: '3. Tenant responsibilities',
      body:
          'The Tenant business is responsible for: the accuracy of '
          'catalog, pricing, and compliance information it enters; '
          'assigning staff roles and PINs appropriately and revoking '
          'access when staff leave; complying with applicable tax, '
          'consumer-protection, and data-privacy law in how it uses the '
          'app; and the physical security of its devices.',
    ),
    LegalSection(
      heading: '4. Acceptable use',
      body:
          'You agree not to: use the app to process transactions you '
          'know to be fraudulent; attempt to bypass, disable, or '
          'interfere with the app\'s security, sync, or licensing '
          'mechanisms; or use the app in a way that violates applicable '
          'law, including BIR receipt and reporting requirements.',
    ),
    LegalSection(
      heading: '5. Fees & billing',
      body:
          'Where a Tenant\'s deployment involves subscription or license '
          'fees, those terms are set out separately at signup or in a '
          'commercial agreement with the Tenant; this document does not '
          'itself establish pricing.',
    ),
    LegalSection(
      heading: '6. Availability & offline operation',
      body:
          'The app is designed to keep working offline and sync when '
          'connectivity returns, but we do not guarantee uninterrupted, '
          'error-free operation. Scheduled maintenance, connectivity '
          'issues, or device problems may affect availability of sync, '
          'reporting, or other server-dependent features.',
    ),
    LegalSection(
      heading: '7. Intellectual property',
      body:
          'Purch.io, its name, logo, and underlying software remain the '
          'property of their respective owners. Tenants retain ownership '
          'of the business data (catalog, transactions, customer records) '
          'they enter into the system.',
    ),
    LegalSection(
      heading: '8. Disclaimer of warranties',
      body:
          'The app is provided "as is" without warranties of any kind, '
          'express or implied, including fitness for a particular '
          'purpose. While the app is built to support BIR-compliant '
          'receipt formatting, the Tenant remains solely responsible for '
          'its own tax compliance and accuracy of filings.',
    ),
    LegalSection(
      heading: '9. Limitation of liability',
      body:
          'To the maximum extent permitted by law, Purch.io and its '
          'providers are not liable for indirect, incidental, or '
          'consequential damages arising from use of the app, including '
          'lost sales, lost data, or regulatory penalties, except where '
          'such limitation is not permitted by applicable law.',
    ),
    LegalSection(
      heading: '10. Termination',
      body:
          'A Tenant may stop using the app at any time. We may suspend or '
          'terminate access for a Tenant that materially breaches these '
          'terms, including fraudulent use or failure to pay applicable '
          'fees, subject to any notice period in a separate commercial '
          'agreement.',
    ),
    LegalSection(
      heading: '11. Governing law',
      body:
          'These terms are governed by the laws of the Republic of the '
          'Philippines, without regard to conflict-of-law principles.',
    ),
    LegalSection(
      heading: '12. Changes to these terms',
      body:
          'We may update these terms as the product evolves. Material '
          'changes will be reflected here with an updated effective date; '
          'continued use of the app after a change constitutes '
          'acceptance of the revised terms.',
    ),
  ];
}
