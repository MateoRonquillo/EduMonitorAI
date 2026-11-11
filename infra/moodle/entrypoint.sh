#!/bin/sh
set -eu
cd /var/www/html
rm -f /var/www/moodledata/.edumonitor-ready
if [ -f config-volume/config.php ]; then cp config-volume/config.php config.php; fi
if [ ! -f config.php ]; then
 php admin/cli/install.php --non-interactive --agree-license --lang=en --wwwroot="$MOODLE_WWWROOT" --dataroot=/var/www/moodledata --dbtype=mysqli --dbhost=moodle-db --dbname=moodle --dbuser=moodle --dbpass="$MOODLE_DB_PASSWORD" --fullname='EduMonitor Laboratory' --shortname=EduMonitor --adminuser=admin --adminpass="$MOODLE_ADMIN_PASSWORD" --adminemail=admin@example.invalid
 cp config.php config-volume/config.php
fi
chown www-data:www-data config.php
chmod 640 config.php
php admin/cli/upgrade.php --non-interactive
php /opt/bootstrap.php
chown -R www-data:www-data /var/www/moodledata
touch /var/www/moodledata/.edumonitor-ready
exec docker-php-entrypoint "$@"
